import { useState, useEffect, useCallback } from 'react'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ReferenceLine, ResponsiveContainer, Legend } from 'recharts'
import './App.css'

const UI_VERSION = '2.2.0'

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
  suspect: boolean
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

  const chartData = [...results].reverse().map(r => {
    const d = new Date(r.timestamp + 'Z')
    return {
      ...r,
      label: d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
        + ' ' + d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' }),
    }
  })

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

      {/* Speed chart */}
      {chartData.length > 1 && (
        <div className="chart-panel">
          <h2>Speed Over Time</h2>
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={chartData} margin={{ top: 10, right: 20, left: 0, bottom: 5 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
              <XAxis
                dataKey="label"
                tick={{ fill: '#475569', fontSize: 11 }}
                tickLine={{ stroke: '#1e293b' }}
                axisLine={{ stroke: '#1e293b' }}
                interval="preserveStartEnd"
              />
              <YAxis
                tick={{ fill: '#475569', fontSize: 12 }}
                tickLine={{ stroke: '#1e293b' }}
                axisLine={{ stroke: '#1e293b' }}
                unit=" Mbps"
              />
              <Tooltip
                contentStyle={{
                  backgroundColor: '#131926',
                  border: '1px solid #334155',
                  borderRadius: '8px',
                  color: '#e2e8f0',
                  fontSize: '13px',
                }}
                labelStyle={{ color: '#94a3b8', marginBottom: '4px' }}
                formatter={(value: number, name: string) => [
                  `${value} Mbps`,
                  name === 'downloadMbps' ? 'Download' : name === 'uploadMbps' ? 'Upload' : name
                ]}
              />
              <Legend
                formatter={(value) =>
                  value === 'downloadMbps' ? 'Download' : value === 'uploadMbps' ? 'Upload' : value
                }
                wrapperStyle={{ fontSize: '13px', color: '#94a3b8' }}
              />
              <ReferenceLine
                y={100}
                stroke="#ef4444"
                strokeDasharray="6 4"
                label={{ value: '100 Mbps promised', fill: 'rgba(239,68,68,0.6)', fontSize: 11, position: 'right' }}
              />
              <Line
                type="monotone"
                dataKey="downloadMbps"
                stroke="#3b82f6"
                strokeWidth={2}
                dot={{ fill: '#3b82f6', r: 3 }}
                activeDot={{ r: 5, stroke: '#60a5fa', strokeWidth: 2 }}
              />
              <Line
                type="monotone"
                dataKey="uploadMbps"
                stroke="#22c55e"
                strokeWidth={2}
                dot={{ fill: '#22c55e', r: 2 }}
                activeDot={{ r: 4, stroke: '#4ade80', strokeWidth: 2 }}
              />
            </LineChart>
          </ResponsiveContainer>
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
                <tr key={r.id} className={r.suspect ? 'suspect-row' : ''}>
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
