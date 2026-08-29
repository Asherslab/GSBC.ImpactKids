---
title: The Elvanto API — doc pages, and where they are wrong
kind: reference
status: current
module: elvanto
verified: 2026-08-29
code:
  - GSBC.ImpactKids.Grpc/Features/Elvanto/ElvantoServices
---

# The Elvanto API — doc pages, and where they are wrong

**Start here before touching anything that talks to Elvanto.** The published docs are thin, and this
account departs from them in ways that fail loudly at best and silently at worst. Everything below
was measured against the live account, not read off a page.

## The doc pages

| Page | URL |
|---|---|
| API index | <https://www.elvanto.com/api/> |
| **People** — every people endpoint | <https://www.elvanto.com/api/people/> |
| `people/getAll` — fields and filters | <https://www.elvanto.com/api/people/getAll/> |
| `people/getInfo` | <https://www.elvanto.com/api/people/getInfo/> |
| `people/search` | <https://www.elvanto.com/api/people/search/> |
| `people/create` | <https://www.elvanto.com/api/people/create/> |
| `people/edit` | <https://www.elvanto.com/api/people/edit/> |
| `people/customFields/getAll` | <https://www.elvanto.com/api/people/customFields/getAll/> |
| People fields reference | <https://www.elvanto.com/api/people-fields/> |
| Services | <https://www.elvanto.com/api/services/> |
| Groups | <https://www.elvanto.com/api/groups/> |

Everything is `POST` to `https://api.elvanto.com/v1/<endpoint>.json` with HTTP Basic auth, whatever
the page implies. `ElvantoService.SendMessage` is the one place that happens.

## How to probe it safely

Reads are free and are the fastest way to settle an argument about a field. The key is
`Elvanto:Authentication` in `GSBC.ImpactKids.Grpc/appsettings.Development.json` (untracked and
gitignored), and it is base64-encoded whole — not `user:pass` assembled from parts:

```
Authorization: Basic base64(<the Authentication string verbatim>)
```

**Writes are a different matter.** They are gated by `Elvanto:AllowWrites` plus a per-endpoint flag
and a process-lifetime budget, and there is a halt-and-verify gate that must be satisfied first —
see the write-testing doc before turning any of it on. Never probe a write by hand.

## What the API returns, as against what it documents

### `fields` is not a menu of everything

The `fields` array on `people/getAll` requests *optional* fields. Asking for something that is
returned by default is sometimes fine and sometimes fatal, and there is no way to tell from the
docs:

- **`gender` — must be named in `fields`.** It is *not* returned by default, in spite of what an
  earlier reading of this account suggested: a `getAll` without it omits the key entirely, whether
  or not a `fields` array is supplied at all. Measured 2026-08-29 against the live account. Leaving
  it out is silent — the call succeeds, `ElvantoPerson.Gender` binds null for every person, and the
  gender sync does nothing while looking healthy. Confirmed by exactly that: `GenderDescriptor`
  shipped without it and wrote 1735 null snapshots and zero inbound rows.
- **`picture` — rejected outright**: `code 250: A field does not exist (picture)`. The whole call
  fails, so a page fetch returns nothing and the roll looks empty. It is returned by default.
  **Never add it to the `Fields` array in `ElvantoService.FetchPageWithRetries`.**

### `gender`

Values are exactly `"Male"`, `"Female"` and `""`. Writable, but only nested under `fields` on
`create`/`edit`, never at the top level. Coverage in this account is poor — about a third of
children have it blank.

On a read it has to be **requested** by name in `fields`, per the section above. `gender` is
therefore the one field in this app that must be named in the array on the way in *and* nested under
`fields` on the way out.

### `picture` — read-only, and mostly not a photo

Returned on every person. Three shapes, and only the third is a real upload:

| Shape | Meaning |
|---|---|
| `cdn.elvanto.com.au/img/default-avatar.svg` | no photo |
| `secure.gravatar.com/avatar/…?d=…default-member-avatar.png` | no photo — a gravatar fallback |
| `d2dek0x2lg6bxh.cloudfront.net/VY9YW40G/members/<id>_thumb_<unix-ts>.jpg` | a real upload |

Real photos are 500×500 JPEG, 7–53 KB, publicly fetchable without auth.

**Over half of the real URLs are broken by Elvanto itself.** A botched migration on their side
concatenated a whole second URL into the thumb suffix — `…_thumb_https://d1o7yryu40l3o2.cloudfront.net/…`
— and those 403 permanently. `people/getInfo` returns the identical broken URL, so there is no
repair path and a retry is wasted. Filter on `_thumb_http` and treat a 403 as final.

**There is no way to write a picture.** Neither `people/edit` nor `people/create` accepts a
`picture` parameter, at the top level or under `fields`. Photos can be read from Elvanto; they
cannot be pushed back.

### Fields that must ride under `fields`

`birthday`, `gender`, `anniversary`, `marital_status`, `access_permissions`, every `custom_<id>`,
and — despite not being a custom field — `school_grade`. Sent at the top level, `school_grade` is
refused with `A param does not exist (school_grade)`. See `ElvantoPersonFields` for the rest,
including why school grade must be sent as the grade **id** even though the docs call it a name,
and why a "select" custom field is a plain string here rather than the array the docs describe.

### `date_modified`

Returned on every people response and **cannot be requested by name** — asking for it is rejected as
a field that does not exist. It is `yyyy-MM-dd HH:mm:ss` in UTC, empty for a person never edited
since creation, and it is per person rather than per field. `ElvantoPerson.LastChangedAtUtc` falls
back to `date_added`.

## Related

- [Gender, person photos, and the photos tool](../../work/2026-08-gender-and-person-photos.md) —
  where the `gender` and `picture` measurements above were taken, and what is being built on them.
- `GSBC.ImpactKids.Grpc/Features/People/Sync/` — the sync engine. Field behaviour lives on the
  descriptors, and each one documents what was measured for its field.
