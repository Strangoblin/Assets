#!/bin/bash
# PASS: run.log 包含 LINK_OK
grep -q "LINK_OK" run.log && exit 0 || exit 1
