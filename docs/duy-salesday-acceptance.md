# DUY-2.0 - SalesDay Acceptance

## Local result (2026-11-18)

Base: develop 7de77d4. Branch: feature/duy-salesday-acceptance.

- Fixed failed GET current retry: retry performs another lookup, not just
  clearing the error and exposing the open form with unknown server state.
- Unknown day state is shown explicitly. Network-failed POST retry keeps the
  existing idempotency key, covered by the existing browser regression test.
- 48 unit tests and 8 browser tests passed. Production build, format check
  and generated API sync passed. Lint: 0 errors, 41 warnings.
- Browser tests intercept HTTP; these are NOT evidence of real SQL acceptance.

## Real staging acceptance (pending)

Coordinate with Phuong and Hai before opening a day: this changes test data.
Use an approved staging account; never commit its password or access token.

1. Hai records the deployed web/API revision. Phuong confirms migrations,
   a designated test business date and an account with salesday.manage.
2. Log in at staging and open /sales-day. Confirm current day matches the
   backend before any write. If an active day exists, opening must be blocked;
   do not close or delete it merely to run this test.
3. On an approved clean test database/date, enter opening cash and open once.
   Confirm DANG_BAN, date and opening cash. Phuong verifies the stored row.
4. Refresh the day via its reload button. Confirm persisted values, then reload
   the page and verify the shared auth session behavior on the deployed build.
5. With two approved test sessions prepared before step 3, submit the same
   business date concurrently: only one day may be created; the other session
   must show the conflict reason and reload current state. Capture redacted
   evidence, not credentials or bearer tokens.
6. Check a staff account without salesday.manage cannot open a day. Verify
   lookup failure/retry and blocked-day messages without changing other data.

Record PASS/FAIL/SKIP, tester, timestamp, deployed revision and redacted
evidence per step. Missing accounts or deployment are SKIP, never PASS.
Only close DUY-2.0 after review/merge and real staging evidence.
