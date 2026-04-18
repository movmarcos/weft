---
name: Bug report
about: Report an unexpected failure during validate / plan / deploy / refresh / restore-history / inspect
title: "[BUG] "
labels: bug
assignees: ''
---

**What happened**
A clear description of the unexpected behaviour.

**Command that failed**
```
weft <command> <args...>
```

**Exit code**
<Paste from `echo $?` / `$LASTEXITCODE`>

**Environment**
- Weft version (or commit SHA):
- .NET SDK (`dotnet --version`):
- OS:

**Target**
- Power BI Premium capacity? Fabric workspace?
- Model size (# tables, # partitions on largest fact):

**Plan output (redact secrets)**
```
<paste `weft plan` output or ./artifacts/<timestamp>-*-plan.tmsl>
```

**Expected vs. actual**
