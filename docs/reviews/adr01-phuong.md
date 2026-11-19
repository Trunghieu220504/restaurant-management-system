# Review ADR-01 — phần Phương (hỗ trợ HIE-1.2)

Báo cáo này làm theo "Hướng dẫn Phương và Hải phối hợp hoàn thành HIE-1.2 / ADR-01" (soạn 12/11/2026),
mục 4 (P1–P4). Đây là bằng chứng và đề xuất cho Hiếu, **không phải ADR đã duyệt**. Các chữ ký helper ở mục 4
chỉ là đề xuất; quyết định thuộc ADR-01 (`docs/adr/ADR-01-shared-foundation.md`, do Hiếu tạo).

## 0. Bảng bằng chứng

| Mục | Giá trị |
| --- | --- |
| Người, ngày | Phương (agent Claude soạn và điều phối thay mặt theo yêu cầu), 12/11/2026 |
| Máy/môi trường | Máy Phương: Windows x64, .NET SDK 10.0.302 (khớp `global.json` 10.0.300 + `latestPatch`), git 2.55.0.windows.4, Docker Engine 29.7.2, SQL Server từ image `mcr.microsoft.com/mssql/server:2022-latest` (Id `sha256:cf8f0ab2…`, build 03/10/2026). Phân tích code làm trên workspace Linux của agent |
| Nhánh, SHA đã test | `docs/phuong-adr01-review`, tách từ `origin/develop` `17244f5673a114bd9d5cb313b057d5d3e94ab1b7` (trùng mốc tham chiếu của hướng dẫn) + thay đổi chưa commit của báo cáo này |
| Phạm vi và file đã đọc/sửa | Đọc: mục 1 của hướng dẫn + các file ở P1. Sửa: file này, `tests/Restaurant.IntegrationTests/PhuongWeekOneSqlServerTests.cs` (thêm 4 test), `docs/AGENT_PROGRESS.md` |
| Lệnh kiểm tra | `scripts/Test-PhuongWeek1.ps1` (restore `--locked-mode`, build Release, `dotnet test --filter FullyQualifiedName~PhuongWeekOneSqlServerTests`) chạy 1 lượt rồi 20 lượt tuần tự qua `TestResults/PhuongAdr01/run-adr01-p2.ps1` (không commit, nằm trong thư mục gitignore). `git diff --check` (đạt). `dotnet format whitespace --folder --verify-no-changes` cho file test (đạt) |
| Passed / failed / skipped | Lượt đơn 12/11 01:25: **9 passed / 0 failed / 0 skipped** (`total = executed = 9`, `notExecuted = 0`); build Release 0 warning, 0 error. Lặp: **20/20 lượt exit 0, mỗi lượt 9/9**, tổng 180 lần chạy test, 0 failed, 0 notExecuted. TRX cũ 10/11 18:49 đối chiếu lại: 5/5 |
| Link Actions + artifact | Backend CI run [43064234818](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/43064234818) (push `develop` 17244f5): job "Identity SQL Server tests" success, artifact `identity-sql-test-results` (id 13875211250). Agent chưa tải được artifact (không có token GitHub) → Hải xác nhận ở H2. 4 test mới chưa chạy trên CI vì chưa push |
| Kết luận | G1: **Đạt trên máy Phương** (SQL Server thật, không skip, 20 lượt lặp); phần CI chờ Hải. G2: **Đạt phía Phương** (mục 2), chờ Hiếu xác nhận |
| Việc chặn và người xử lý | (1) Đọc TRX SQL của CI, nhất là sau khi PR này chạy CI — Hải (H2). (2) Chưa có PR ADR-01 — Hiếu |
| SHA ADR đã review | Chưa có. Đến 12/11/2026 trên GitHub chưa có PR ADR-01/HIE-1.2 và chưa có `docs/adr/` trên `develop` |

## 1. Phạm vi

Đã đọc: `AGENTS.md`, `CLAUDE.md`, `docs/AGENT_PROGRESS.md`, `CONTRIBUTING.md`, `docs/phan-cong-chi-tiet-7-tuan.md`
(HIE-1.2, PHU-1.1, HAI-1.3, mục 5 quy ước), `docs/superpowers/specs/2026-11-05-restaurant-mvp-scope.md`,
`docs/superpowers/specs/2026-11-05-database-revised-design.md` (mục 5–8, C03), `docs/phuong-tuan-1.md`,
`docs/spike-c03-result.md`, `.github/workflows/backend-ci.yml`, `scripts/Test-PhuongWeek1.ps1`, mã nguồn SalesDay,
Catalog, `Program.cs`, `GlobalExceptionHandler.cs`, `DependencyTests.cs` và `web/src/features/sales-day/`.

Không sửa workflow chung, không sửa logic SalesDay, không thêm Outbox/worker.

Viết tắt đường dẫn trong báo cáo:

- `BD` = `src/Modules/SalesDay/Restaurant.Modules.SalesDay.Infrastructure/BusinessDays.cs`
- `EP` = `src/Modules/SalesDay/Restaurant.Modules.SalesDay.Presentation/SalesDayEndpoints.cs`
- `BC` = `src/Modules/SalesDay/Restaurant.Modules.SalesDay.Application/BusinessDayContracts.cs`
- `CTX` = `src/Modules/SalesDay/Restaurant.Modules.SalesDay.Infrastructure/Persistence/SalesDayDbContext.cs`
- `T` = `tests/Restaurant.IntegrationTests/PhuongWeekOneSqlServerTests.cs` (số dòng theo nhánh này)
- `FE` = `web/src/features/sales-day/api.ts`

## 2. P1 — Bản đồ phần đang có

### 2.1 Idempotency key lưu ở đâu, phạm vi nào, xử lý ra sao?

| Câu hỏi | Hiện trạng | Tham chiếu |
| --- | --- | --- |
| Nơi lưu | Ngay trên dòng nghiệp vụ: cột `salesday.NgayBan.RequestKey` (`uniqueidentifier`), không có bảng yêu cầu riêng | `BusinessDay.RequestKey`; `CTX:25`; migration `InitialSalesDay` dòng 54–59 (`IX_NgayBan_RequestKey` unique) |
| Phạm vi | Toàn bảng `NgayBan`, chỉ cho thao tác mở ngày. Không gắn theo người hay endpoint: một khóa chỉ dùng được cho đúng một ngày | `CTX:25`; `BD:23` |
| Nhận khóa | Header `Idempotency-Key` phải parse được thành `Guid` khác rỗng, nếu không trả 400 `salesday.idempotency.required`. `Guid.TryParse` nhận cả dạng không gạch nối; mock FE chỉ nhận dạng có gạch nối | `EP:19-20`; `web/src/features/sales-day/mock.ts:22-29` |
| Thứ tự kiểm tra | Validate input (400) → mở transaction → lấy khóa `open` → tra khóa → kiểm tra ngày đang hoạt động → kiểm tra ngày | `BD:18-28` |
| Cùng khóa, cùng nội dung | So sánh `BusinessDate`, `OpeningCash` **và người mở** (`OpenedBy == actor`). Khớp → `Replayed = true` → HTTP 200 | `BD:25-26`; `EP:23` |
| Cùng khóa, khác nội dung hoặc khác người | 409 `salesday.idempotency.conflict` | `BD:26`; `EP:22` |
| Nội dung trả khi replay | Trạng thái **hiện tại** của ngày (`Map(old)`), không phải bản chụp response lần đầu. Nếu ngày đã sang `CHO_CHOT`/`DA_CHOT` thì replay trả trạng thái mới | `BD:10`, `BD:26` |
| Yêu cầu thất bại | Không được ghi nhớ. Cùng khóa bị 409 `previous_not_closed` hôm nay có thể thành công sau khi ngày cũ đã chốt | `BD:27-28` (trả về trước `SaveChanges`) |
| Đồng thời cùng khóa | Tra khóa nằm sau khóa ứng dụng `open` nên các request trùng được tuần tự hóa; unique index là lớp chặn cuối (nếu bị vượt sẽ ra `DbUpdateException` → 500) | `BD:22-23`; `CTX:25` |

So với thiết kế dữ liệu mục 6 (`YeuCauDaXuLy`: PK `(Operation, ActorScope, Key)`, `RequestHash`, `ResultReference`, `State`):
SalesDay **chưa** dùng mô hình đó. Cách hiện tại đủ cho "một khóa tạo một aggregate", chưa đủ cho lệnh nhiều bước/nhiều module.

### 2.2 Mã lỗi, HTTP status và trường frontend dùng

| `code` | HTTP | Nơi phát sinh | FE |
| --- | --- | --- | --- |
| `salesday.idempotency.required` | 400 | `EP:19-20` | `FE:54` |
| `salesday.input.invalid` | 400 | `BD:18-19` (tiền âm, ngày mặc định, actor/key rỗng) | `FE:56` |
| `salesday.idempotency.conflict` | 409 | `BD:26` | `FE:57` |
| `salesday.previous_not_closed` | 409 | `BD:27` (còn ngày `DANG_BAN`/`CHO_CHOT`) | `FE:59` |
| `salesday.date.invalid` | 409 | `BD:28` (ngày mới không sau mọi ngày đã có) | `FE:61` |
| không có `code` | 401 | `EP:18` (`Results.Unauthorized()`) hoặc middleware xác thực | `FE:66-67` theo status |
| không có `code` | 403 | `EP:24` (policy `permission = salesday.manage`) | `FE:68` theo status |
| không có `code` | 500 | Hết 10 s chờ khóa `open` → `THROW 51000` (`BD:22`) → `GlobalExceptionHandler` trả ProblemDetails chỉ có `title` + `traceId` | rơi vào câu mặc định `FE:71` |

Hình dạng lỗi: ProblemDetails RFC 7807, `title = code` và extension `code` (`EP:30-31`). Frontend **chỉ đọc
`body.code` và HTTP status** (`FE:89-91`, `FE:64-75`), không đọc `title`/`detail`.

Lưu ý: `EP:22` ánh xạ status bằng so sánh chuỗi (`input.invalid` → 400, mọi mã khác → 409). Thêm mã mới sẽ mặc định 409.

### 2.3 Khóa ngày: tên resource, Shared/Exclusive, owner, timeout

| Nội dung | Mã chạy thật (production) | Chỉ có trong test |
| --- | --- | --- |
| Resource | `restaurant:salesday:open` — một cổng toàn cục cho thao tác mở ngày (`BD:22`) | `C03:test` (`T:83`) |
| Mode | Chỉ `Exclusive` | `Shared` và `Exclusive` theo tham số (`T:81-86`) |
| Owner | `Transaction` (giải phóng khi commit/rollback) | `Transaction` |
| Timeout | 10 000 ms; kết quả `< 0` → `THROW` → 500 | Tham số; test trả về mã của `sp_getapplock` để assert |

**Chưa có khóa ngày Shared/Exclusive trong mã chạy thật.** Không có helper, không có quy ước tên resource cho
"cổng ngày", chưa có lệnh ghi theo ngày nào (order, thanh toán, kho) hay lệnh chốt ngày. Khóa C03 hiện chỉ được
chứng minh ở mức `sp_getapplock` trong test.

### 2.4 Hai DbContext dùng chung connection/transaction

| | Production | Test C03 |
| --- | --- | --- |
| Số DbContext | 1 (`SalesDayDbContext`) | 2 (`SalesDayDbContext` + `CatalogDbContext`) |
| Ai mở connection/transaction | EF: `db.Database.BeginTransactionAsync` (`BD:20`) | Code gọi: `new SqlConnection` + `BeginTransactionAsync` (`T:107-108`) |
| Context tham gia thế nào | — | `UseSqlServer(connection)` + `UseTransactionAsync(transaction)` (`T:109-111`) |
| Commit | `tx.CommitAsync` (`BD:32`) | Không có test commit |
| Rollback | Ngầm định khi `await using` dispose ở các nhánh `return` sớm hoặc khi có exception (`BD:20`, `BD:24-28`) | Code gọi `transaction.RollbackAsync()` (`T:116`) |

Kết luận: cơ chế "chủ lệnh mở và commit, các context chỉ enlist" **mới có trong test**. Context của test được tạo
bằng `UseSqlServer(...)` trần (`T:17-18`), không qua `SalesDayPersistence.Configure` (bảng lịch sử migration theo
schema) và không có execution strategy, nên chưa phản ánh đúng cấu hình production.

### 2.5 Test chứng minh gì, chưa chứng minh gì

| Test | Chứng minh | Chưa chứng minh |
| --- | --- | --- |
| `Opening_is_serialized_and_replay_does_not_create_another_day` (`T:38`) | Hai request mở ngày khác ngày, khác khóa chạy đồng thời → đúng 1 thành công, 1 `previous_not_closed`, 1 dòng | Phần replay/conflict nằm trong `if` (`T:50`): chỉ chạy khi request thứ nhất thắng, khoảng 50% số lần **bỏ qua mà vẫn xanh** |
| `Database_rejects_a_second_active_day_even_without_the_service` (`T:58`) | Filtered unique index chặn ngày hoạt động thứ hai khi ghi thẳng | — |
| `Catalog_constraints_reject_negative_price_and_invalid_option_range` (`T:67`) | Check constraint giá và khoảng tùy chọn | — |
| `C03_shared_locks_coexist_and_exclusive_waits_until_transactions_end` (`T:88`) | Hai Shared cùng tồn tại; Exclusive bị từ chối khi Shared đang giữ; hai Exclusive không cùng vào | Mọi lần từ chối dùng `LockTimeout = 0` (`T:96`, `T:100`): **chưa chứng minh Exclusive thật sự chờ** rồi được cấp. `T:96-98` là nâng cấp Shared→Exclusive trong cùng transaction, không phải mẫu nên dùng |
| `C03_two_contexts_share_one_transaction_and_rollback_both_modules` (`T:105`) | Ghi ở hai module rồi rollback chủ động → không còn dữ liệu ở cả hai | Lỗi giữa chừng ở module thứ hai; nhánh commit |

Không có test nào cho: tầng HTTP SalesDay (status, `code`, header, 401/403), timeout khóa `open` (→ 500), retry/deadlock,
mức cô lập giao dịch, nhiều instance API, lệnh chốt ngày (test 14, 15 của ma trận thiết kế). Spike này **không** được
suy rộng thành bằng chứng cho luồng tiền/kho.

## 3. P2 — Chạy lại C03 trên SQL Server thật

### 3.1 Test bổ sung trong nhánh này

| Test mới | Lấp khoảng trống |
| --- | --- |
| `Replay_with_the_same_key_returns_the_original_day_and_other_payloads_conflict` (`T:124`) | Replay luôn chạy (không phụ thuộc thứ tự): cùng khóa → `Replayed`, cùng Id; khác tiền, khác ngày, **khác người** → conflict; vẫn 1 dòng |
| `Concurrent_retries_with_the_same_key_create_exactly_one_day` (`T:150`) | 5 retry cùng ý định chạy đồng thời → 1 tạo mới, 4 replay, cùng Id, 1 dòng |
| `C03_exclusive_waits_for_a_shared_holder_and_is_granted_after_release` (`T:170`) | Exclusive với timeout 1,5 s thật sự chờ rồi trả -1; Exclusive timeout 15 s vẫn chờ khi Shared còn giữ, được cấp với mã `1` (cấp sau khi chờ) ngay khi Shared rollback |
| `C03_failure_in_the_second_module_rolls_back_the_first_module_write` (`T:193`) | SalesDay ghi xong, Catalog lỗi khóa ngoại → rollback → không còn dữ liệu ở cả hai module |

Trạng thái: biên dịch với `TreatWarningsAsErrors` (0 warning) và đạt cả 21 lượt chạy ở mục 3.4. Test chờ Exclusive
mất 2,024–2,039 s mỗi lượt, khớp với 1,5 s timeout + 0,5 s quan sát, tức là khóa thật sự chờ chứ không bị từ chối ngay.

### 3.2 Đối chiếu các tình huống bắt buộc

| Tình huống | Test | Trạng thái |
| --- | --- | --- |
| Hai Shared cùng ngày | `T:88` | Đạt 21/21 lượt (12/11) |
| Exclusive khi Shared đang giữ | `T:88` (không chờ), `T:170` (chờ/timeout/cấp sau chờ) | Đạt 21/21 lượt |
| Hai Exclusive cùng ngày | `T:88` | Đạt 21/21 lượt |
| Rollback giao dịch hai DbContext | `T:105`, `T:193` | Đạt 21/21 lượt |
| Mở ngày đồng thời | `T:38` | Đạt 21/21 lượt (lưu ý: phần replay bên trong chỉ chạy khi request đầu thắng, xem 2.5) |
| Retry cùng ý định | `T:124`, `T:150` | Đạt 21/21 lượt |

Giới hạn: 21 lượt trên một máy, một instance SQL Server Docker, mức cô lập mặc định, không tải song song.
Kết quả này không chứng minh các điểm ở 4.3 (retry strategy, deadlock nhiều resource, lệnh chốt ngày, tầng HTTP).

### 3.3 Lệnh cho Phương chạy trên máy mình

Đọc `scripts/Test-PhuongWeek1.ps1` trước. Docker Desktop phải đang chạy. Từ repo root, trên nhánh này:

```powershell
powershell -NoProfile -File scripts/Test-PhuongWeek1.ps1
[xml]$trxReport = Get-Content TestResults/PhuongWeek1/phuong-week1.trx
$trxReport.TestRun.ResultSummary.Counters
```

Đạt khi `total = executed = passed = 9`, `failed = 0`, `notExecuted = 0`.

Lặp 20 lần **tuần tự** (mỗi lần script tạo và xóa container riêng; không chạy song song), lưu TRX khác tên:

```powershell
$runs = 'TestResults/PhuongWeek1/runs'
New-Item -ItemType Directory -Force $runs | Out-Null
'run,exit' | Set-Content "$runs/summary.csv"
for ($i = 1; $i -le 20; $i++) {
    powershell -NoProfile -File scripts/Test-PhuongWeek1.ps1
    $code = $LASTEXITCODE
    if (Test-Path TestResults/PhuongWeek1/phuong-week1.trx) {
        Move-Item -Force TestResults/PhuongWeek1/phuong-week1.trx ("$runs/run-{0:D2}.trx" -f $i)
    }
    "$i,$code" | Add-Content "$runs/summary.csv"
}
Get-ChildItem "$runs/*.trx" | ForEach-Object {
    [xml]$t = Get-Content $_.FullName
    $c = $t.TestRun.ResultSummary.Counters
    [pscustomobject]@{ Run = $_.Name; Total = $c.total; Passed = $c.passed; Failed = $c.failed; NotExecuted = $c.notExecuted }
} | Format-Table
```

Đạt khi cả 20 dòng trong `summary.csv` có `exit = 0` và mọi TRX có `passed = 9`, `failed = 0`, `notExecuted = 0`.
TRX nằm trong `TestResults` (gitignore): không commit, gửi qua kênh nhóm sau khi rà soát không có chuỗi kết nối.

### 3.4 Kết quả ngày 12/11/2026 trên máy Phương

| Mục | Giá trị |
| --- | --- |
| Máy, SDK, Docker, SQL Server | Windows x64; .NET SDK 10.0.302; Docker Engine 29.7.2; `mssql/server:2022-latest` (Id `sha256:cf8f0ab2…`) |
| SHA đã chạy | `17244f5` + thay đổi chưa commit của nhánh `docs/phuong-adr01-review` |
| Lượt đơn (01:23–01:25): total/executed/passed/failed/notExecuted | 9 / 9 / 9 / 0 / 0; build Release 0 warning, 0 error |
| 20 lượt (01:25–01:32): số lượt exit 0 / tổng | 20 / 20; mọi TRX 9 / 9 / 9 / 0 / 0 |
| Thời gian mỗi test qua 20 lượt | `C03_exclusive_waits…` 2,024–2,039 s; các test khác 0,026–0,464 s |
| Lỗi đầu tiên | Không có |
| Kết luận G1 | **Đạt** trên máy Phương; xác nhận trên CI chờ Hải |

Bằng chứng nằm trong `TestResults/PhuongAdr01/` (gitignore): `run-log.txt`, `single.log`, `single.trx`,
`runs/run-01..20.trx`, `runs/run-01..20.log`, `runs/summary.csv`, `previous-phuong-week1.trx` (bản 10/11).

## 4. P3 — Đề xuất cho Hiếu

### 4.1 Giữ nguyên (frontend đang dùng)

1. Header `Idempotency-Key` là UUID khác rỗng; thiếu hoặc sai → 400 `salesday.idempotency.required`.
2. ProblemDetails có extension `code`; giữ nguyên 5 mã và status ở mục 2.2. ADR có thể **thêm** trường
   (`traceId`, `detail`) nhưng không đổi tên/đổi chỗ `code`, không đổi status của mã đang có.
3. 201 khi tạo, 200 khi replay, cùng hình dạng `BusinessDayResponse` (`BC:4`); tiền `long` VND, `businessDate`
   `yyyy-MM-dd`, `openedAt` UTC, trạng thái `DANG_BAN`/`CHO_CHOT`/`DA_CHOT`.
4. Ý nghĩa idempotency: cùng khóa + cùng nội dung + cùng người → replay; khác bất kỳ phần nào → 409 conflict.
   Nếu ADR chọn khóa chính `(Operation, ActorScope, Key)` như thiết kế mục 6 thì "khác người, cùng khóa" sẽ thành
   **yêu cầu mới** thay vì conflict — đây là thay đổi hành vi, cần ghi rõ trong ADR và test lại.
5. Lớp chặn ở database: filtered unique `IX_NgayBan_ActiveSlot`, `CK_NgayBan_State` (`CHO_CHOT` vẫn chiếm chỗ),
   unique `RequestKey` (`CTX:19`, `CTX:24-26`).

### 4.2 Có thể dùng chung (chữ ký đề xuất, chưa phải quyết định)

**A. Helper khóa ứng dụng SQL.** Phần trùng thật: gọi `sp_getapplock` và xử lý mã trả về (đang có 2 bản: `BD:22`
và `T:81-86`). Cần EF/SqlClient nên đặt ở hạ tầng dùng chung, **không** đặt trong Contracts.

```csharp
public enum AppLockMode { Shared, Exclusive }
public enum AppLockResult { Granted = 0, GrantedAfterWait = 1, Timeout = -1, Cancelled = -2, Deadlock = -3, Error = -999 }

public static class SqlAppLock
{
    // Ném lỗi nếu database.CurrentTransaction == null. Luôn @LockOwner = 'Transaction', @Resource là tham số.
    public static Task<AppLockResult> AcquireAsync(
        DatabaseFacade database, string resource, AppLockMode mode, TimeSpan timeout, CancellationToken ct);
}
```

Nơi gọi: `BusinessDays.OpenAsync` (`restaurant:salesday:open`, Exclusive, 10 s); các lệnh ghi theo ngày của
Sales/Billing/Inventory (Shared); lệnh chốt ngày (Exclusive). Trả kết quả thay vì `THROW` để tầng trên ánh xạ
`Timeout` thành mã lỗi có `code` (ví dụ `salesday.busy`, status do ADR chọn) thay vì 500. Thêm mã mới thì Duy cần
cập nhật `FE:53-63`.

**B. Cổng ngày cho module khác** — `Restaurant.Modules.SalesDay.Contracts`, chỉ dùng kiểu BCL, không gói NuGet:

```csharp
public sealed record ActiveDayLease(Guid DayId, DateOnly BusinessDate);
public sealed record DayGateResult(ActiveDayLease? Day, string? ErrorCode); // salesday.no_active_day | salesday.closing | salesday.busy

public interface ISalesDayGate
{
    // Gọi bên trong transaction dùng chung đã mở. Lấy Shared trên cổng ngày TRƯỚC, rồi mới đọc trạng thái ngày.
    Task<DayGateResult> EnterForWriteAsync(CancellationToken ct);
}
```

Tên resource cần ADR chốt một trong hai:
(i) cố định `restaurant:salesday:day-gate` — chỉ có một ngày hoạt động nên đủ, không phải đọc ngày trước khi khóa;
(ii) theo ngày `restaurant:salesday:day:{DayId:N}` — phải đọc ngày trước khi khóa nên **bắt buộc đọc lại trạng thái
sau khi khóa**. Đề xuất (i) cho MVP.

**C. Transaction dùng chung giữa các DbContext** — hiện chỉ có trong test (`T:107-111`, `T:197-201`):

```csharp
public interface IModuleTransaction : IAsyncDisposable // scoped theo request/lệnh
{
    DbConnection Connection { get; }
    DbTransaction Transaction { get; }
    Task EnlistAsync(DbContext context, CancellationToken ct); // SetDbConnection + UseTransactionAsync
    Task CommitAsync(CancellationToken ct);                     // chỉ handler mở lệnh được gọi
}
```

Quy tắc kèm theo: handler mở lệnh là chủ (mở, commit); dispose khi chưa commit = rollback; context đã enlist không
tự `BeginTransaction`/`Commit`; thứ tự khóa theo C03: cổng ngày → order/phiên → hóa đơn → thanh toán → kho.
Vì cần EF, project chứa interface này nằm ngoài 4 project của module; `DependencyTests` hiện chỉ cho
Infrastructure tham chiếu Domain + Application cùng module nên phải mở rộng có kiểm soát.

**D. Kho idempotency** (chỉ khi ADR muốn dùng chung cho lệnh tiền/kho):

```csharp
public enum IdempotencyOutcome { NotFound, SameRequest, Conflict }
public sealed record IdempotencyLookup(IdempotencyOutcome Outcome, string? ResultReference);

public interface IIdempotencyStore
{
    // Gọi sau khi đã lấy khóa, trong cùng transaction với thay đổi nghiệp vụ.
    Task<IdempotencyLookup> FindAsync(string operation, Guid actorScope, Guid key, byte[] requestHash, CancellationToken ct);
    void Record(string operation, Guid actorScope, Guid key, byte[] requestHash, string resultReference);
}
```

SalesDay có thể giữ cách hiện tại; nếu chuyển sang kho chung thì giữ nguyên hành vi ở 4.1.

### 4.3 Chưa đủ bằng chứng (và test đề xuất)

| Điểm | Vì sao chưa đủ | Test/việc đề xuất |
| --- | --- | --- |
| Timeout khóa `open` | Ra 500 không có `code` (`BD:22`) | Test giữ khóa `open` ở connection khác, gọi `OpenAsync` → mã lỗi có `code` (sau khi có helper A) |
| Retry/execution strategy | Chưa bật `EnableRetryOnFailure`. Nếu ADR bật, `BeginTransactionAsync` ở `BD:20` sẽ ném `InvalidOperationException` trừ khi bọc trong `CreateExecutionStrategy().ExecuteAsync(...)` | Test `OpenAsync` với context bật retry; retry phải dùng lại cùng idempotency key |
| Deadlock | `sp_getapplock` trả -3 chưa được test; chưa có lệnh khóa nhiều resource | Test hai lệnh khóa theo đúng thứ tự C03 không deadlock |
| Nâng cấp Shared→Exclusive | `T:96-98` làm trong cùng transaction; hai writer cùng nâng cấp sẽ deadlock | ADR ghi: lệnh chốt lấy Exclusive ngay từ đầu, không nâng cấp |
| Order / chốt ngày | Chưa có nghiệp vụ thật nên chưa thể có test 14, 15 của ma trận thiết kế | Khi có lệnh chốt: hai người chốt → một snapshot; writer đến muộn chờ rồi thấy `CHO_CHOT`; rollback giữa lúc chốt không để lại dữ liệu dở |
| Mức cô lập | Test chạy READ COMMITTED mặc định. Với SNAPSHOT, đọc ngày trước khi khóa sẽ thấy dữ liệu cũ | ADR chốt mức cô lập cho lệnh ghi theo ngày; test nếu bật RCSI |
| Tầng HTTP | Không có test endpoint SalesDay | Test qua `ApiFactory`: 201/200/400/409 kèm `code`, 401, 403, header sai dạng |
| Replay trả trạng thái hiện tại | `BD:26` trả `Map(old)` | ADR quyết định: chấp nhận, hay lưu bản chụp response |
| Thất bại không được ghi nhớ | `BD:27-28` | ADR quyết định có lưu `State` thất bại như `YeuCauDaXuLy` không |
| Mock FE lệch backend | `mock.ts:37-41` không so người mở | Báo Duy; không phải lỗi backend |

### 4.4 Review Outbox ở mức tính nguyên tử

Hiện **chưa có** interface, bảng hay worker Outbox (không có kết quả khi tìm `outbox` trong `src`/`tests`).
Điều kiện tối thiểu để chấp nhận phần Outbox của ADR-01:

1. Bản ghi Outbox ghi qua cùng `IModuleTransaction` với thay đổi nghiệp vụ: commit cùng nhau, rollback cùng nhau.
2. Test: rollback → 0 dòng Outbox; commit → đúng 1 dòng; replay cùng idempotency key → không thêm dòng.
3. Nếu ADR mới có interface thì ghi "chưa triển khai", không xác nhận đã xong. Worker, retry, giám sát thuộc task sau.

Phương không thêm bảng hay worker Outbox trước khi ADR xác định phạm vi.

## 5. P4 — Checklist review PR ADR-01 (dùng khi Hiếu gửi PR/SHA)

Chưa có PR để review. Khi có, đọc đúng SHA được gửi và kiểm tra:

- [ ] Project Contracts không có `PackageReference`/`FrameworkReference`; Domain/Application không kéo EF hay
      Infrastructure qua Contracts.
- [ ] `tests/Restaurant.ArchitectureTests/DependencyTests.cs` được cập nhật trong cùng PR: cho phép đúng tham chiếu
      Contracts mới và không hơn. Hiện test chỉ kiểm 4 project mỗi module (`Every_module_has_four_isolated_projects`),
      nên cần thêm kiểm tra cho project Contracts và project dùng chung.
- [ ] Mục 4.1 được giữ nguyên, hoặc có ghi thay đổi + kế hoạch cập nhật FE.
- [ ] Helper khóa: owner Transaction, resource là tham số, xử lý timeout có `code`; tên resource cổng ngày đã chốt.
- [ ] Ai mở/commit/rollback transaction dùng chung; thứ tự khóa C03.
- [ ] Outbox đạt 3 điều kiện ở 4.4 hoặc được ghi là chưa triển khai.
- [ ] Các điểm 4.3 chưa thống nhất nằm trong mục Open questions của ADR.

Request changes nếu thiếu điều kiện bắt buộc; chỉ Approve khi đã xử lý và test liên quan có bằng chứng.

## 6. Việc tiếp theo

1. Phương: commit file này, file test và `docs/AGENT_PROGRESS.md` (stage đúng từng file), push nhánh, mở draft PR vào `develop` liên kết HIE-1.2.
2. Hải: xác nhận TRX SQL của CI trên PR này có đủ 9 test `PhuongWeekOneSqlServerTests`, không skip (H2). Sau đó Phương cập nhật `docs/spike-c03-result.md`.
3. Hiếu: dùng mục 2 và 4 khi viết ADR-01; gửi PR/SHA để Phương làm mục 5.
