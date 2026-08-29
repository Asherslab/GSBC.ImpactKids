---
title: People
kind: reference
status: current
module: people
verified: 2026-08-29
code:
  - GSBC.ImpactKids.Grpc/Features/People
  - GSBC.ImpactKids.WASM/Features/People
---

# People

The person record and the things attached to it.

| Doc | Read it before |
|---|---|
| [School grades and who is in the program](school-grades-and-programs.md) | changing a grade list or an age rule |
| [Gender](gender.md) | touching the gender field or its descriptor — null is a real answer, and `gender` must be asked for by name on a read |
| [Person photos](photos.md) | touching the photo endpoints, the capture view or the S3 client — two of its settings fail silently if changed |

Where photo objects physically live, and what backs them up, is
[the object store](../infrastructure/object-store.md).
