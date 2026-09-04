#!/bin/bash
# Phase 1 — migration inventory and architecture contract baseline.
set -euo pipefail
cd "$(dirname "$0")"
python3 verify.py
