#!/usr/bin/env python3
from __future__ import annotations
import argparse, csv, html
from collections import Counter
from pathlib import Path


def main() -> int:
    p=argparse.ArgumentParser()
    p.add_argument('--ledger', default='Docs/Architecture/Overhaul/FINAL_RUNTIME_MIGRATION_LEDGER.csv')
    p.add_argument('--output', default='Docs/Architecture/Overhaul/OverhaulDashboard.html')
    args=p.parse_args()
    ledger=Path(args.ledger)
    rows=list(csv.DictReader(ledger.open(encoding='utf-8')))
    phases=Counter(r['migration_phase'] for r in rows)
    actions=Counter(r['final_action'] for r in rows)
    def esc(v): return html.escape(str(v or ''))
    body=['<!doctype html><meta charset="utf-8"><title>Chantilly Architecture Overhaul</title>',
          '<style>body{font:14px system-ui;margin:2rem;color:#1f2937}table{border-collapse:collapse;width:100%}th,td{border:1px solid #d1d5db;padding:.45rem;vertical-align:top}th{background:#111827;color:white;position:sticky;top:0}.cards{display:flex;gap:1rem;flex-wrap:wrap}.card{border:1px solid #9ca3af;border-radius:8px;padding:1rem;min-width:220px;background:#f9fafb}code{font-size:12px}</style>',
          '<h1>Chateau Chantilly architecture-overhaul dashboard</h1>',
          f'<p>{len(rows)} runtime files are explicitly classified.</p><div class="cards">']
    body.append('<div class="card"><b>By phase</b><ul>'+''.join(f'<li>{esc(k)}: {v}</li>' for k,v in sorted(phases.items()))+'</ul></div>')
    body.append('<div class="card"><b>By action</b><ul>'+''.join(f'<li>{esc(k)}: {v}</li>' for k,v in actions.most_common())+'</ul></div></div>')
    cols=['migration_phase','current_file','final_action','target_owner_or_path','serialized_reference_count','deletion_or_completion_gate','required_test_evidence']
    body.append('<h2>Runtime migration ledger</h2><table><thead><tr>'+''.join(f'<th>{esc(c)}</th>' for c in cols)+'</tr></thead><tbody>')
    for r in sorted(rows,key=lambda x:(x['migration_phase'],x['current_file'])):
        body.append('<tr>'+''.join(f'<td>{esc(r.get(c,""))}</td>' for c in cols)+'</tr>')
    body.append('</tbody></table>')
    out=Path(args.output); out.parent.mkdir(parents=True,exist_ok=True); out.write_text(''.join(body),encoding='utf-8')
    print(f'Wrote {out}')
    return 0
if __name__=='__main__': raise SystemExit(main())
