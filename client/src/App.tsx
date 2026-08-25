import { useState, useCallback } from 'react'
import './App.css'

const UI_VERSION = '1.0.0'

type TestPhase = 'idle' | 'ping' | 'download' | 'upload' | 'done'

interface TestResult {
  ping: number
  download: number
  upload: number
}

function App() {
  const [phase, setPhase] = useState<TestPhase>('idle')
  const [result, setResult] = useState<TestResult>({ ping: 0, download: 0, upload: 0 })
  const [progress, setProgress] = useState(0)
  const [history, setHistory] = useState<(TestResult & { timestamp: Date })[]>([])

  const runTest = useCallback(async () => {
    setPhase('ping')
    setProgress(0)
    const newResult: TestResult = { ping: 0, download: 0, upload: 0 }

    // --- Ping (median of 5) ---
    const pings: number[] = []
    for (let i = 0; i < 5; i++) {
      const start = performance.now()
      await fetch('/api/ping')
      pings.push(performance.now() - start)
      setProgress((i + 1) / 5 * 100)
    }
    newResult.ping = Math.round(pings.sort((a, b) => a - b)[2])
    setResult({ ...newResult })

    // --- Download (progressive chunks) ---
    setPhase('download')
    setProgress(0)
    const downloadSizes = [2_000_000, 5_000_000, 10_000_000, 25_000_000]
    let bestDown = 0

    for (let i = 0; i < downloadSizes.length; i++) {
      const size = downloadSizes[i]
      const start = performance.now()
      const resp = await fetch(`/api/download?size=${size}`)
      const blob = await resp.blob()
      const elapsed = (performance.now() - start) / 1000
      const mbps = (blob.size * 8 / 1_000_000) / elapsed
      if (mbps > bestDown) bestDown = mbps
      newResult.download = Math.round(bestDown * 100) / 100
      setResult({ ...newResult })
      setProgress((i + 1) / downloadSizes.length * 100)
    }

    // --- Upload (progressive chunks) ---
    setPhase('upload')
    setProgress(0)
    const uploadSizes = [2_000_000, 5_000_000, 10_000_000, 25_000_000]
    let bestUp = 0

    for (let i = 0; i < uploadSizes.length; i++) {
      const size = uploadSizes[i]
      const data = new Uint8Array(size)
      crypto.getRandomValues(data)
      const start = performance.now()
      await fetch('/api/upload', {
        method: 'POST',
        body: data,
      })
      const elapsed = (performance.now() - start) / 1000
      const mbps = (size * 8 / 1_000_000) / elapsed
      if (mbps > bestUp) bestUp = mbps
      newResult.upload = Math.round(bestUp * 100) / 100
      setResult({ ...newResult })
      setProgress((i + 1) / uploadSizes.length * 100)
    }

    setPhase('done')
    setProgress(100)
    setHistory(prev => [{ ...newResult, timestamp: new Date() }, ...prev].slice(0, 10))
  }, [])

  const getPhaseLabel = () => {
    switch (phase) {
      case 'ping': return 'Testing Latency...'
      case 'download': return 'Testing Download...'
      case 'upload': return 'Testing Upload...'
      case 'done': return 'Test Complete'
      default: return 'Ready'
    }
  }

  return (
    <div className="app">
      <header>
        <h1>Speed Test</h1>
        <span className="version">ui:{UI_VERSION}</span>
      </header>

      <div className="test-panel">
        <div className="status">{getPhaseLabel()}</div>

        {phase !== 'idle' && (
          <div className="progress-bar">
            <div className="progress-fill" style={{ width: `${progress}%` }} />
          </div>
        )}

        <div className="results">
          <div className={`metric ${phase === 'ping' ? 'active' : ''}`}>
            <div className="metric-value">{result.ping}</div>
            <div className="metric-unit">ms</div>
            <div className="metric-label">Ping</div>
          </div>
          <div className={`metric ${phase === 'download' ? 'active' : ''}`}>
            <div className="metric-value">{result.download.toFixed(2)}</div>
            <div className="metric-unit">Mbps</div>
            <div className="metric-label">Download</div>
          </div>
          <div className={`metric ${phase === 'upload' ? 'active' : ''}`}>
            <div className="metric-value">{result.upload.toFixed(2)}</div>
            <div className="metric-unit">Mbps</div>
            <div className="metric-label">Upload</div>
          </div>
        </div>

        <button
          className="start-btn"
          onClick={runTest}
          disabled={phase !== 'idle' && phase !== 'done'}
        >
          {phase === 'idle' ? 'Start Test' : phase === 'done' ? 'Run Again' : 'Testing...'}
        </button>
      </div>

      {history.length > 0 && (
        <div className="history">
          <h2>History</h2>
          <table>
            <thead>
              <tr>
                <th>Time</th>
                <th>Ping</th>
                <th>Download</th>
                <th>Upload</th>
              </tr>
            </thead>
            <tbody>
              {history.map((h, i) => (
                <tr key={i}>
                  <td>{h.timestamp.toLocaleTimeString()}</td>
                  <td>{h.ping} ms</td>
                  <td>{h.download.toFixed(2)} Mbps</td>
                  <td>{h.upload.toFixed(2)} Mbps</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <footer>
        Server: {window.location.hostname}
      </footer>
    </div>
  )
}

export default App
