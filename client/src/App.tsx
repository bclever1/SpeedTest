import { useState, useEffect, useCallback } from 'react'
import './App.css'

const UI_VERSION = '2.0.0'

interface TestResult {
  id: number
  timestamp: string
  downloadMbps: number
  uploadMbps: number
  pingMs: number
  jitter: number
  packetLoss: number
  isp: string
  serverName: string
  serverLocation: string
  resultUrl: string
}

interface Stats {
  count: number
  download?: { avg: number; min: number; max: number; median: number }
  upload?: { avg: number; min: number; max: number; median: number }
  ping?: { avg: number; min: number; max: number }
  isp?: string
}

function App() {
  const [results, setResults] = useState<TestResult[]>([])
  const [stats, setStats] = useState<Stats>({ count: 0 })
  const [days, setDays] = useState(7)
  const [running, setRunning] = useState(false)
  const [chartHeight] = useState(200)

  const fetchData = useCallback(async () => {
    const [resultsRes, statsRes] = await Promise.all([
      fetch(`/api/results?days=${days}`),
      fetch(`/api/stats?days=${days}`)
    ])
    setResults(await resultsRes.json())
    setStats(await statsRes.json())
  }, [days])

  useEffect(() => {
    fetchData()
    const interval = setInterval(fetchData, 60000)
    return () => clearInterval(interval)
  }, [fetchData])

  const runManualTest = async () => {
    setRunning(true)
    try {
      await fetch('/api/test', { method: 'POST' })
      await fetchData()
    } finally {
      setRunning(false)
    }
  }

  const formatDate = (ts: string) => {
    const d = new Date(ts + 'Z')
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
  }

  const formatTime = (ts: string) => {
    const d = new Date(ts + 'Z')
    return d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })
  }

  // Chart data — show last N points reversed so oldest is on the left
  const chartData = [...results].reverse()
  const maxDown = Math.max(...chartData.map(r => r.downloadMbps), 1)
  const chartMax = Math.ceil(maxDown / 50) * 50 || 100

  return (
    <div className="app">
      <header>
        <div>
          <h1>ISP Monitor</h1>
          {stats.isp && <span className="isp">{stats.isp}</span>}
        </div>
        <div className="header-right">
          <span className="version">ui:{UI_VERSION}</span>
          <button className="run-btn" onClick={runManualTest} disabled={running}>
            {running ? 'Testing...' : 'Run Test Now'}
          </button>
        </div>
      </header>

      {stats.count > 0 && stats.download && stats.upload && stats.ping && (
        <div className="stats-grid">
          <div className="stat-card">
            <div className="stat-label">Avg Download</div>
            <div className="stat-value">{stats.download.avg}</div>
            <div className="stat-unit">Mbps</div>
            <div className="stat-range">{stats.download.min} — {stats.download.max}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Avg Upload</div>
            <div className="stat-value">{stats.upload.avg}</div>
            <div className="stat-unit">Mbps</div>
            <div className="stat-range">{stats.upload.min} — {stats.upload.max}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Avg Ping</div>
            <div className="stat-value">{stats.ping.avg}</div>
            <div className="stat-unit">ms</div>
            <div className="stat-range">{stats.ping.min} — {stats.ping.max}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Tests Run</div>
            <div className="stat-value">{stats.count}</div>
            <div className="stat-unit">in {days}d</div>
            <div className="stat-range">median: {stats.download.median} Mbps</div>
          </div>
        </div>
      )}

      {/* Promised vs Actual banner */}
      {stats.count > 0 && stats.download && (
        <div className={`promise-banner ${stats.download.avg >= 100 ? 'good' : 'bad'}`}>
          <div className="promise-label">
            AT&T Promised: <strong>100–300 Mbps</strong>
          </div>
          <div className="promise-actual">
            Your Average: <strong>{stats.download.avg} Mbps</strong>
            {stats.download.avg < 100 && (
              <span className="promise-deficit">
                {' '}({Math.round((1 - stats.download.avg / 100) * 100)}% below minimum)
              </span>
            )}
          </div>
        </div>
      )}

      {/* Download speed chart */}
      {chartData.length > 1 && (
        <div className="chart-panel">
          <h2>Download Speed Over Time</h2>
          <div className="chart-container">
            <div className="chart-y-axis">
              <span>{chartMax}</span>
              <span>{Math.round(chartMax / 2)}</span>
              <span>0</span>
            </div>
            <div className="chart">
              <div className="chart-promise-line" style={{ bottom: `${(100 / chartMax) * chartHeight}px` }}>
                <span>100 Mbps promised</span>
              </div>
              <svg viewBox={`0 0 ${chartData.length * 20} ${chartHeight}`} preserveAspectRatio="none" className="chart-svg">
                <polyline
                  fill="none"
                  stroke="#3b82f6"
                  strokeWidth="2"
                  points={chartData.map((r, i) =>
                    `${i * 20 + 10},${chartHeight - (r.downloadMbps / chartMax) * chartHeight}`
                  ).join(' ')}
                />
                {chartData.map((r, i) => (
                  <circle
                    key={i}
                    cx={i * 20 + 10}
                    cy={chartHeight - (r.downloadMbps / chartMax) * chartHeight}
                    r="3"
                    fill="#3b82f6"
                  >
                    <title>{`${formatDate(r.timestamp)} ${formatTime(r.timestamp)}: ${r.downloadMbps} Mbps`}</title>
                  </circle>
                ))}
              </svg>
            </div>
          </div>
        </div>
      )}

      {/* Time range selector */}
      <div className="range-selector">
        {[1, 3, 7, 14, 30].map(d => (
          <button
            key={d}
            className={days === d ? 'active' : ''}
            onClick={() => setDays(d)}
          >
            {d}d
          </button>
        ))}
      </div>

      {/* Results table */}
      {results.length > 0 && (
        <div className="results-table">
          <h2>Test Results</h2>
          <table>
            <thead>
              <tr>
                <th>Time</th>
                <th>Download</th>
                <th>Upload</th>
                <th>Ping</th>
                <th>Server</th>
              </tr>
            </thead>
            <tbody>
              {results.map(r => (
                <tr key={r.id}>
                  <td>
                    <div>{formatDate(r.timestamp)}</div>
                    <div className="time-sub">{formatTime(r.timestamp)}</div>
                  </td>
                  <td className={r.downloadMbps < 100 ? 'bad-value' : 'good-value'}>
                    {r.downloadMbps} Mbps
                  </td>
                  <td>{r.uploadMbps} Mbps</td>
                  <td>{r.pingMs} ms</td>
                  <td className="server-cell">
                    {r.serverName}
                    <div className="time-sub">{r.serverLocation}</div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {results.length === 0 && (
        <div className="empty">
          No test results yet. The first automated test will run shortly, or click "Run Test Now".
        </div>
      )}
    </div>
  )
}

export default App
