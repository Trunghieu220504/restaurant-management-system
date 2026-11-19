# Hướng dẫn Phương và Hải phối hợp hoàn thành HIE-1.2 / ADR-01

> Cập nhật 14/11/2026: đây là hướng dẫn phối hợp lịch sử. ADR-01 đã merge ở PR #29 và người phụ trách xác nhận đã duyệt. Các bước tạo/duyệt ADR bên dưới không phải việc cần làm lại; review hiện tại áp dụng cho code implementation. Trạng thái mới nhất ở `shared-foundation-handoff.md`.

Ngày soạn: 12/11/2026. Phạm vi: hỗ trợ Hiếu chốt nền tảng dùng chung trong tuần 1.

## 1. Đọc trước khi làm

**Tất cả lệnh của Phương chạy trên máy Phương; tất cả lệnh của Hải chạy trên máy Hải.** Không điều khiển máy Hiếu, không dùng đường dẫn `C:/Users/MSI/...`, không lấy database hoặc bí mật trên máy Hiếu. Tài liệu này không tự chạy lệnh hoặc triển khai hệ thống.

Không thể bảo đảm kết quả chỉ bằng một danh sách lệnh. Mỗi bước bên dưới có điều kiện đạt, bằng chứng và cách xử lý khi không đạt. Chưa kiểm chứng thì ghi `Chưa kiểm chứng`; không ghi `Done` chỉ vì đã commit hoặc CI có màu xanh.

Đây là **hướng dẫn phối hợp**, không phải ADR-01 đã được duyệt. File thiết kế dự kiến do Hiếu tạo là `docs/adr/ADR-01-shared-foundation.md`. Không tự coi các lựa chọn Contracts/Outbox trong tài liệu này là quyết định kiến trúc đã chốt.

Nguồn cần đọc trong checkout của mỗi người:

- `AGENTS.md`, `docs/AGENT_PROGRESS.md`, `CONTRIBUTING.md`.
- `docs/phan-cong-chi-tiet-7-tuan.md`: HIE-1.2, PHU-1.1 và HAI-1.3.
- `docs/superpowers/specs/2026-11-05-restaurant-mvp-scope.md`.
- `docs/superpowers/specs/2026-11-05-database-revised-design.md`, đặc biệt C03.
- `docs/phuong-tuan-1.md`, `docs/spike-c03-result.md`.
- `.github/workflows/backend-ci.yml`, `.github/workflows/web-ci.yml`.

**Mốc tham chiếu của tài liệu:** mã nguồn cục bộ dựa trên `develop` tại `17244f5`; không thay thế trạng thái GitHub lúc thực hiện. Backend CI ở mốc này đã lọc `FullyQualifiedName~SqlServer`; Web CI đã có. Phải fetch và đọc lại trước khi sửa, tránh làm trùng công việc đã merge.

## 2. Phân công và đầu ra

| Người | Trách nhiệm chính | Đầu ra trong repo/GitHub |
|---|---|---|
| Hiếu | Viết ADR-01, quyết định đề xuất, triển khai nền tảng và cập nhật test kiến trúc | PR HIE-1.2 vào develop; ADR có quyết định, lý do, giới hạn và phương án kiểm chứng |
| Phương | Chứng minh nghiệp vụ/giao dịch/khóa ngày đang dùng; review thiết kế để tái sử dụng đúng | `docs/reviews/adr01-phuong.md`, test bổ sung nếu cần, bằng chứng C03, review PR Hiếu |
| Hải | Chứng minh CI chạy đủ test; review cấu hình, triển khai và vận hành | `docs/reviews/adr01-hai.md`, PR CI nếu phát hiện thiếu, links Actions/artifacts và review PR Hiếu |

Tên file và nhánh trong hướng dẫn là quy ước đề xuất cho đợt phối hợp này, không phải file đã tồn tại. Có PR cùng mục tiêu đang mở thì tiếp tục PR đó, không tạo bản trùng.

**Không giao Phương/Hải viết lại nền tảng thay Hiếu.** Phương không sửa workflow chung; Hải không tự chuyển logic nghiệp vụ SalesDay. Thay đổi ranh giới cần ghi rõ trong PR và nhờ chủ phần code review.

## 3. Chuẩn bị trên máy riêng

### 3.1 Checkout sạch và lấy mã mới

Nên dùng thư mục ngoài OneDrive/Google Drive để tránh khóa file. Ví dụ PowerShell, chỉ dành cho máy chưa có checkout:

```powershell
New-Item -ItemType Directory -Force C:\dev | Out-Null
Set-Location C:\dev
git clone https://github.com/Trunghieu220504/restaurant-management-system.git
Set-Location restaurant-management-system
```

Nếu đã có checkout, mở terminal tại thư mục chứa `RestaurantManagement.slnx`, không clone chồng lên thư mục cũ.

Chạy **từng lệnh**, kiểm tra kết quả trước lệnh kế tiếp:

```powershell
git status --short --branch
git remote -v
git fetch origin
```

Nếu có thay đổi chưa commit: lưu công việc trên nhánh đang làm theo quy trình của mình trước, không `reset --hard`, `clean` hoặc ép đổi nhánh. Nếu Git hỏi xóa thư mục thất bại, dừng chuỗi lệnh và kiểm tra ứng dụng đang giữ file; không xóa thủ công thư mục mã nguồn.

Phương tạo nhánh từ remote mới nhất, không cần chuyển qua develop cục bộ cũ:

```powershell
git switch -c docs/phuong-adr01-review --no-track origin/develop
```

Hải dùng nhánh riêng:

```powershell
git switch -c chore/hai-adr01-verification --no-track origin/develop
```

Nếu nhánh đã tồn tại, không thêm `-C` hoặc xóa nhánh. Kiểm tra `git branch -vv`, tiếp tục nhánh đúng rồi cập nhật develop khi working tree sạch.

Ghi `git rev-parse HEAD` vào báo cáo; thêm một dòng nhận việc trong `docs/AGENT_PROGRESS.md` với người, nhánh, phạm vi và trạng thái `In Progress`.

### 3.2 Công cụ và dữ liệu thử

```powershell
git --version
dotnet --version
Get-Content global.json
docker version
```

Hải kiểm tra thêm `node --version`, `npm --version` và đối chiếu phiên bản Node trong Web CI/Dockerfile của checkout mới nhất. Dùng SDK theo `global.json`, không tự đổi phiên bản toàn repo để phù hợp máy mình.

SQL test chỉ được dùng SQL Server thử nghiệm có quyền tạo database riêng. Không trỏ `IDENTITY_TEST_SQLSERVER` vào production, staging đang có dữ liệu vận hành hoặc database dùng chung của nhóm. Không gửi connection string, mật khẩu, token, khóa SSH lên PR hoặc chat.

## 4. Phương: thu thập bằng chứng và review nghiệp vụ

### P1. Lập bản đồ phần đang có

Đọc và dẫn đường dẫn/hàm thực tế trong báo cáo:

- `src/Modules/SalesDay/Restaurant.Modules.SalesDay.Infrastructure/BusinessDays.cs`.
- `src/Modules/SalesDay/Restaurant.Modules.SalesDay.Application/BusinessDayContracts.cs`.
- `src/Modules/SalesDay/Restaurant.Modules.SalesDay.Presentation/SalesDayEndpoints.cs`.
- SalesDayDbContext, migration SalesDay và Catalog.
- `tests/Restaurant.IntegrationTests/PhuongWeekOneSqlServerTests.cs`.

Trả lời rõ:

1. Idempotency key lưu ở đâu, phạm vi nào, cùng key/cùng payload và cùng key/khác payload xử lý thế nào?
2. Mã lỗi nào đã tồn tại? HTTP status và trường `code` nào frontend đang dùng?
3. Khóa ngày có tên resource gì, Shared/Exclusive khi nào, owner là transaction hay session, timeout xử lý ra sao?
4. Hai DbContext dùng chung connection/transaction như thế nào? Ai mở, commit và rollback?
5. Test hiện tại chứng minh điều gì, chưa chứng minh điều gì? Không suy rộng một spike sang toàn bộ luồng tiền/kho.

**Đạt:** bảng đối chiếu có tham chiếu code và test, không chỉ nhận xét chung “đã ổn”.

### P2. Chạy lại C03 trên SQL Server thật

Với Docker Desktop đã chạy, thực thi script có sẵn từ repo root:

```powershell
powershell -NoProfile -File scripts/Test-PhuongWeek1.ps1
```

Script hiện tạo container tên ngẫu nhiên, bind SQL vào loopback, dùng mật khẩu tạm và xóa đúng container nó tạo trong `finally`. Đọc script trước khi chạy; không chạy Docker prune hoặc xóa container khác. Kết quả tại `TestResults/PhuongWeek1/phuong-week1.trx`.

Script đặt `IDENTITY_TEST_SQLSERVER` trong thời gian chạy rồi khôi phục biến cũ. Vì vậy chạy `dotnet test` sau khi script kết thúc **không tự được thừa hưởng SQL đó**.

Đọc bộ đếm TRX:

```powershell
[xml]$trxReport = Get-Content TestResults/PhuongWeek1/phuong-week1.trx
$trxReport.TestRun.ResultSummary.Counters
```

**Đạt:** số test đúng với các case được chọn, `failed=0`, `notExecuted=0`. Mốc cũ có 5 test, không ép số 5 nếu test mới đã được bổ sung. Test bị skip vì thiếu biến không được tính đạt.

Các tình huống bắt buộc phải có bằng chứng:

| Tình huống | Điều cần chứng minh |
|---|---|
| Hai Shared cùng ngày | Có thể cùng tồn tại |
| Exclusive khi Shared đang giữ | Chờ/timeout đúng, không vượt khóa |
| Hai Exclusive cùng ngày | Không cùng vào vùng được khóa |
| Rollback giao dịch hai DbContext | Không còn dữ liệu ghi dở ở cả hai module |
| Mở ngày đồng thời | Chỉ một ngày hoạt động; request còn lại có kết quả hợp lệ |
| Retry cùng ý định | Không tạo thêm ngày hoặc ghi thêm dữ liệu |

Theo hàng đợi tuần 1, lặp các kịch bản đồng thời ít nhất 20 lần và lưu kết quả từng lần. Có thể chạy script 20 lần tuần tự, kiểm tra exit code và lưu bản TRX khác tên sau mỗi lần vì script dùng cùng tên file. Không chạy nhiều bản script cùng lúc để “tăng tải” ngoài phạm vi đã thiết kế. Không tắt test không ổn định để lấy màu xanh.

### P3. Đề xuất cho Hiếu

Trong `docs/reviews/adr01-phuong.md`, viết ba nhóm:

- **Giữ nguyên:** hợp đồng API, mã lỗi, ý nghĩa idempotency mà frontend đã sử dụng.
- **Có thể dùng chung:** phần khóa/giao dịch thật sự trùng, kèm chữ ký helper đề xuất và nơi gọi.
- **Chưa đủ bằng chứng:** retry/deadlock, order/chốt ngày chưa có nghiệp vụ thật; đề xuất test cần bổ sung.

Review Outbox ở mức tính nguyên tử: dữ liệu nghiệp vụ và sự kiện phải cùng commit/rollback; không xác nhận triển khai đầy đủ nếu mới có interface. Không tự thêm worker hoặc bảng Outbox trước khi ADR xác định phạm vi.

### P4. Review PR ADR-01 của Hiếu

Khi Hiếu gửi PR/SHA, đọc đúng phiên bản đó. Kiểm tra Contracts không làm Domain/Application kéo theo EF hoặc project Infrastructure; tham chiếu `tests/Restaurant.ArchitectureTests/DependencyTests.cs`. Cấu trúc Contracts chưa được duyệt thì ghi ý kiến, không tự coi là ngoại lệ kiến trúc.

Ghi góp ý bằng file/dòng và tác động nghiệp vụ. Dùng `Request changes` khi thiếu điều kiện bắt buộc; chỉ `Approve` sau khi các điểm đó đã được xử lý và test liên quan có bằng chứng.

## 5. Hải: CI và khả năng vận hành

### H1. Kiểm tra trạng thái GitHub trước khi sửa

Vào repo → Pull requests/Actions, đọc các PR đang mở liên quan CI. Ghi SHA develop hiện tại và URL run. Kiểm tra workflow thực tế, không dựa vào ghi chú tiến độ cũ.

Ở mốc tài liệu, các job cần đối chiếu gồm `Build and test`, `Identity SQL Server tests`, `Verify API Client Sync`, `Web Build and Test`. Tên job SQL hiện vẫn là Identity nhưng filter đã bao phủ `SqlServer`; không đổi tên tùy tiện vì branch protection có thể đang yêu cầu tên cũ.

### H2. Chứng minh CI không bỏ qua SQL

1. Mở run của SHA đang review và chọn job SQL.
2. Kiểm tra SQL container sẵn sàng, biến `IDENTITY_TEST_SQLSERVER` được truyền đến test.
3. Tải artifact kết quả SQL; đọc TRX và danh sách test.
4. Xác nhận có `PhuongWeekOneSqlServerTests` và `IdentityRefreshTokenSqlServerTests`, cùng các lớp SQL còn lại.
5. Ghi số passed/failed/skipped theo artifact thật, không lấy lại con số 83 từ lần kiểm chứng cũ.

**Đạt:** các case SQL dự kiến được thực thi, không skip, không lỗi. Job không xuất artifact hoặc không chọn được test là chưa đủ bằng chứng dù exit code 0. Nếu thiếu, Hải sửa workflow trong PR riêng, giữ cách test database dùng một lần và không in secret.

### H3. Chạy kiểm tra cục bộ

Từ repo root, chạy lần lượt; lệnh nào lỗi thì dừng và xử lý trước khi đi tiếp:

```powershell
dotnet restore RestaurantManagement.slnx --locked-mode
dotnet build RestaurantManagement.slnx -c Release --no-restore
dotnet format whitespace RestaurantManagement.slnx --verify-no-changes --no-restore
dotnet test RestaurantManagement.slnx -c Release --no-build --no-restore --logger trx --results-directory TestResults/Adr01
git diff --check
```

Bộ test tổng trên máy chỉ xác nhận SQL nếu terminal đã có `IDENTITY_TEST_SQLSERVER` hợp lệ. Nếu không, ghi rõ số skip và lấy bằng chứng SQL từ job CI hoặc một lượt chạy với SQL thử nghiệm. Không tạo kết nối thật trong file được commit.

Khi kiểm tra frontend và đồng bộ API:

```powershell
Set-Location web
npm.cmd ci
npm.cmd run gen:api:check
npm.cmd run lint
npm.cmd run format:check
npm.cmd test
npm.cmd run build
Set-Location ..
```

Trên Linux/macOS dùng `npm` thay `npm.cmd`. Nếu API types lệch, xem OpenAPI thay đổi có chủ đích không, sinh lại bằng `npm run gen:api` rồi review diff; không sửa generated types bằng tay. Cảnh báo phải được ghi riêng, không đổi thành “0 lỗi, 0 cảnh báo” nếu thực tế khác.

### H4. Review vận hành ADR-01

Ghi trong `docs/reviews/adr01-hai.md`:

| Nội dung | Câu hỏi cần trả lời |
|---|---|
| Cấu hình | Giá trị nào qua biến môi trường? Mặc định và validate khi khởi động? |
| Migration | Có schema mới không? Apply trước/sau API? Ai thực hiện? |
| Khôi phục | API cũ còn chạy với schema mới không? Giữ dữ liệu nào khi rollback? |
| Logging | Có correlation ID và lỗi đủ chẩn đoán? Có nguy cơ log token/secret/payload nhạy cảm? |
| Outbox | Retry/backoff, giới hạn retry, bản ghi lỗi, idempotent consumer và nhiều instance được xử lý thế nào? |
| Quan sát | Nhận biết backlog hoặc worker lỗi ra sao? Healthcheck phản ánh gì? |
| Kiểm thử | Mỗi tuyên bố có test hoặc tiêu chí nghiệm thu nào? |

Nếu ADR chỉ định nghĩa abstraction Outbox, đánh dấu phần worker/retry/giám sát là **chưa triển khai**, ghi task tiếp theo; không yêu cầu xây cả hệ thống ngoài phạm vi HIE-1.2.

Bước này không tự cho phép triển khai lên VPS. Chỉ chạy staging khi có PR đã duyệt, hướng dẫn migration và phạm vi triển khai được người phụ trách xác nhận. Chỉ gửi URL và kết quả đã lọc dữ liệu nhạy cảm, không đưa IP quản trị/SSH key vào báo cáo công khai.

### H5. Bảo vệ nhánh và review

Kiểm tra quy tắc hiện hành có áp dụng đúng `main`/`develop`, PR/review và CI bắt buộc. Không tắt bảo vệ để merge nhanh. Nếu thiếu quyền admin, ghi bằng chứng và giao Hiếu/người quản trị cấu hình; không dùng tài khoản người khác để tự duyệt.

Không tự thêm một status check chưa chạy được hoặc bị workflow bỏ qua vào danh sách bắt buộc. Review PR Hiếu trên SHA cuối; khi commit mới làm mất approval, kiểm tra diff mới và duyệt lại.

## 6. Phối hợp song song qua GitHub

### Luồng công việc

1. Hiếu đưa hướng dẫn này lên GitHub qua PR tài liệu. Trước khi có trên remote, Phương/Hải không thể lấy file chỉ bằng pull. Không copy cả working tree của Hiếu sang máy khác.
2. Phương bắt đầu P1/P2; Hải bắt đầu H1/H2 độc lập trên máy riêng.
3. Hai người tạo báo cáo và draft PR vào `develop`, liên kết tới task HIE-1.2. Hiếu dùng bằng chứng để viết ADR-01.
4. Khi ADR có bản nháp, cả hai review cùng SHA; quyết định chưa thống nhất được ghi ở mục Open questions của ADR.
5. Hiếu triển khai phần đã chốt và cập nhật test kiến trúc. Phương review nghiệp vụ; Hải kiểm chứng CI/triển khai.
6. Chỉ merge khi đáp ứng tiêu chí; sau đó cả hai ghi SHA merge và cập nhật checkout của mình.

Nếu ADR chưa có, **không phải chờ để làm P1/P2/H1/H2**. Tuy nhiên không ghi “đã approve ADR” khi chỉ review báo cáo của mình.

### Commit và PR trên máy Phương

Sau khi tự tạo và kiểm tra các file báo cáo:

```powershell
git add docs/reviews/adr01-phuong.md docs/AGENT_PROGRESS.md
git diff --cached --check
git diff --cached
git commit -m "docs: record Phuong ADR-01 transaction review"
git push -u origin docs/phuong-adr01-review
```

Nếu có test mới, stage chính xác file test đó và nêu lý do trong PR; không `git add .`.

### Commit và PR trên máy Hải

```powershell
git add docs/reviews/adr01-hai.md docs/AGENT_PROGRESS.md
git diff --cached --check
git diff --cached
git commit -m "docs: record Hai ADR-01 CI verification"
git push -u origin chore/hai-adr01-verification
```

Nếu sửa workflow, stage từng file thực sự sửa, dùng commit `ci: ...` tương ứng. Không commit file chưa tồn tại theo mẫu. Các lệnh push ở đây dành cho Phương/Hải thực hiện trên máy họ; không phải yêu cầu agent push từ máy Hiếu.

Trên GitHub chọn **base: develop**, **compare: nhánh của mình**. Điền mô tả thật, chọn reviewer khác tác giả, chờ CI và giải quyết góp ý. Với PR đóng góp vào develop, dùng squash merge theo CONTRIBUTING. Không merge thẳng vào main.

### Khi develop thay đổi hoặc có conflict

Đảm bảo đã commit công việc, working tree sạch, rồi:

```powershell
git fetch origin
git merge origin/develop
```

Nếu conflict `docs/AGENT_PROGRESS.md`: giữ nhật ký cả hai bên, cập nhật trạng thái task tương ứng, bỏ marker sau khi gộp thủ công. Không chọn toàn bộ ours/theirs. Nếu conflict code/migration, nhờ chủ module review cách giải; không sinh lại migration đã được triển khai chỉ để hết conflict.

Chạy lại kiểm tra phù hợp, stage file đã giải quyết, commit merge và push **cùng nhánh PR**. Không force push. CI chạy lại và approval có thể phải xin lại. Các bước giải conflict chỉ làm khi Git báo conflict; không tạo merge commit giả khi không có gì thay đổi.

## 7. Mẫu bằng chứng và mô tả PR

Mỗi báo cáo cần có bảng này; điền kết quả thật:

| Mục | Giá trị |
|---|---|
| Người, ngày | ... |
| Máy/môi trường | Windows/Linux, SDK, SQL version; không ghi secret |
| Nhánh, SHA đã test | ... |
| Phạm vi và file đã đọc/sửa | ... |
| Lệnh kiểm tra | ... |
| Passed / failed / skipped | ... |
| Link Actions + artifact | ... |
| Kết luận | Đạt / Không đạt / Chưa kiểm chứng |
| Việc chặn và người xử lý | ... |
| SHA ADR đã review | ... |

Mẫu mô tả PR:

```markdown
## Vấn đề và kết quả
Hỗ trợ HIE-1.2: [phạm vi cụ thể].
Đã xác nhận: [...]. Chưa xác nhận: [...].

## Bằng chứng
- SHA kiểm tra: ...
- Lệnh và số passed/failed/skipped: ...
- Actions/artifact: ...
- Báo cáo: docs/reviews/adr01-....md

## Ảnh hưởng
- Code/schema/cấu hình thay đổi: ... hoặc không có.
- Triển khai/khôi phục: ... hoặc không áp dụng.
- Cần Hiếu/Phương/Hải quyết định: ...
```

Không đưa ảnh chụp có mật khẩu hoặc toàn bộ environment vào PR. TRX nằm trong TestResults bị gitignore; lưu artifact trên CI hoặc gửi bằng kênh nhóm phù hợp sau khi rà soát dữ liệu.

## 8. Điều kiện hoàn thành

| Cổng | Người chịu trách nhiệm | Bằng chứng để đóng |
|---|---|---|
| G1: tái hiện C03 | Phương | Test SQL thật đạt, không skip; kết quả lặp kịch bản đồng thời và giới hạn rõ ràng |
| G2: hiểu cơ chế hiện tại | Phương | Bản đồ Contracts/error/idempotency/lock có code references |
| G3: CI đáng tin cậy | Hải | Run SHA cuối, artifact chứng minh SQL được chạy; client sync/lint/test/build đạt khi áp dụng |
| G4: ADR được thống nhất | Hiếu + Phương + Hải | ADR có quyết định và phạm vi; hai reviewer xác nhận phần phụ trách |
| G5: triển khai nền tảng đúng thiết kế | Hiếu | Test kiến trúc và test hành vi mới đạt, PR được review |
| G6: bàn giao | Cả nhóm | SHA merge, báo cáo và sổ tiến độ cập nhật; không còn chặn bắt buộc |

Phương hoàn tất phần mình khi G1/G2 và review nghiệp vụ G4 đạt. Hải hoàn tất khi G3 và review vận hành G4 đạt. **Toàn bộ HIE-1.2 chỉ hoàn thành khi cả G5/G6 cũng đạt**; hai báo cáo hỗ trợ không thay thế code nền tảng của Hiếu.

Nếu chưa đạt: ghi lỗi đầu tiên, cách tái hiện, SHA, người nhận và bước tiếp theo. Không hứa chắc “đạt hết” trước khi có bằng chứng, không dùng việc bỏ test/tắt ruleset để chuyển trạng thái sang Done.
