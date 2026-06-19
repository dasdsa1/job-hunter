import fs from 'fs';
import path from 'path';
import { RunReport, JobMatch } from './types.js';

export function generateReport(report: RunReport): string {
  const outDir = 'data';
  fs.mkdirSync(outDir, { recursive: true });

  const ts = report.timestamp.replace(/[:.]/g, '-');
  const htmlPath = path.join(outDir, `report-${ts}.html`);
  const jsonPath = path.join(outDir, `report-${ts}.json`);

  fs.writeFileSync(jsonPath, JSON.stringify(report, null, 2), 'utf8');
  fs.writeFileSync(htmlPath, buildHtml(report), 'utf8');

  return htmlPath;
}

function buildHtml(report: RunReport): string {
  const { searchConfig: cfg, jobMatches } = report;
  const applied = jobMatches.filter((m) => m.applied);
  const matched = jobMatches.filter((m) => !m.applied);

  const jobRow = (m: JobMatch, showCoverLetter = false) => {
    const scoreColor =
      m.match.score >= 8 ? '#22c55e' : m.match.score >= 6 ? '#f59e0b' : '#ef4444';
    return `
      <tr>
        <td><a href="${esc(m.job.url)}" target="_blank">${esc(m.job.title)}</a></td>
        <td>${esc(m.job.company)}</td>
        <td>${esc(m.job.location)}</td>
        <td><span class="badge" style="background:${scoreColor}">${m.match.score}/10</span></td>
        <td>${esc(m.job.source)}</td>
        <td>${esc(m.match.summary)}</td>
        ${
          showCoverLetter
            ? `<td><details><summary>View</summary><pre>${esc(m.coverLetter ?? '')}</pre></details></td>`
            : ''
        }
      </tr>`;
  };

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Job Hunt Report — ${esc(report.timestamp)}</title>
  <style>
    body { font-family: system-ui, sans-serif; max-width: 1100px; margin: 2rem auto; padding: 0 1rem; color: #1e293b; }
    h1 { color: #0f172a; }
    h2 { color: #1e40af; margin-top: 2rem; }
    table { width: 100%; border-collapse: collapse; margin-top: 1rem; font-size: 0.9rem; }
    th { background: #1e293b; color: white; padding: 0.5rem 0.75rem; text-align: left; }
    td { padding: 0.5rem 0.75rem; border-bottom: 1px solid #e2e8f0; vertical-align: top; }
    tr:hover td { background: #f8fafc; }
    .badge { color: white; padding: 0.2rem 0.5rem; border-radius: 9999px; font-weight: 600; }
    .stat { display: inline-block; background: #f1f5f9; border-radius: 0.5rem; padding: 1rem 1.5rem; margin: 0.5rem; text-align: center; }
    .stat-num { font-size: 2rem; font-weight: 700; color: #1e40af; }
    pre { white-space: pre-wrap; background: #f8fafc; padding: 0.75rem; border-radius: 0.25rem; font-size: 0.8rem; max-width: 400px; }
    a { color: #2563eb; }
    details summary { cursor: pointer; color: #2563eb; }
    .config-table td:first-child { font-weight: 600; width: 180px; }
  </style>
</head>
<body>
  <h1>Job Hunt Report</h1>
  <p style="color:#64748b">${esc(report.timestamp)}</p>

  <div>
    <div class="stat"><div class="stat-num">${report.totalScraped}</div>Jobs scraped</div>
    <div class="stat"><div class="stat-num">${report.totalMatched}</div>Matched (≥${cfg.minScore})</div>
    <div class="stat"><div class="stat-num">${applied.length}</div>Applied</div>
  </div>

  <h2>Search Configuration</h2>
  <table>
    <tbody class="config-table">
      <tr><td>Job title</td><td>${esc(cfg.jobTitle)}</td></tr>
      <tr><td>Location</td><td>${esc(cfg.location)}</td></tr>
      <tr><td>Keywords</td><td>${esc(cfg.keywords)}</td></tr>
      <tr><td>Sites</td><td>${esc(cfg.sites.join(', '))}</td></tr>
      <tr><td>Min score</td><td>${cfg.minScore}</td></tr>
      <tr><td>Easy Apply only</td><td>${cfg.easyApplyOnly ? 'Yes' : 'No'}</td></tr>
    </tbody>
  </table>

  ${
    applied.length > 0
      ? `<h2>Applied Jobs (${applied.length})</h2>
  <table>
    <thead><tr><th>Title</th><th>Company</th><th>Location</th><th>Score</th><th>Source</th><th>Summary</th><th>Cover Letter</th></tr></thead>
    <tbody>${applied.map((m) => jobRow(m, true)).join('')}</tbody>
  </table>`
      : ''
  }

  <h2>All Matched Jobs (${report.totalMatched})</h2>
  <table>
    <thead><tr><th>Title</th><th>Company</th><th>Location</th><th>Score</th><th>Source</th><th>Summary</th></tr></thead>
    <tbody>${jobMatches.map((m) => jobRow(m, false)).join('')}</tbody>
  </table>
</body>
</html>`;
}

function esc(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}
