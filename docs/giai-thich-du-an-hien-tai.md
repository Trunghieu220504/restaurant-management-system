# Giải thích toàn bộ dự án tại thời điểm hiện tại

> Mốc đối chiếu: ngày 05/11/2026, mã nguồn local tại commit **2bca5f60ae3be16ad00c3ad39dde8421f4643a8c**, nhánh **feature/identity-authentication**. Tài liệu mô tả những gì đọc được trong repository, không đồng nhất thiết kế dự kiến với chức năng đã chạy. Trạng thái GitHub được ghi theo lần kiểm chứng cuối của phiên triển khai trước: Draft PR #8 vào develop, hai job CI đạt, chưa merge. Không truy vấn lại GitHub khi biên soạn tài liệu này.

> **Cập nhật 11/11/2026:** tài liệu này là ảnh chụp ngày 05/11 và **mô tả chủ yếu module Identity**. Từ đó `develop` đã có thêm: Identity được merge (PR #7, #8);
> SalesDay (`GET /api/salesday/current`, `POST /api/salesday/open`, khóa ứng dụng SQL) và migration Catalog của Phương (PR #16); khung `web/` Next.js 16 của Bảo Anh.
> Các câu trong tài liệu nói "chưa có frontend", "chín module còn lại chưa có use case" hay "chưa có khóa ứng dụng/giao dịch chung" **đã lỗi thời một phần**; trạng thái mới nhất ở
> [AGENT_PROGRESS.md](AGENT_PROGRESS.md) mục 2, và hợp đồng API Identity ở `docs/api/identity-contract.md` (khi nhánh `docs/identity-api-contract` được merge).
> Phần giải thích kiến trúc, bốn tầng, Identity và bảo mật vẫn đúng. Giải thích SalesDay xem [phuong-tuan-1.md](phuong-tuan-1.md) và [spike-c03-result.md](spike-c03-result.md).

## Mục lục

1. [Hiện có gì, chưa có gì?](#1-hiện-có-gì-chưa-có-gì)
2. [Công nghệ và kiến trúc](#2-công-nghệ-và-kiến-trúc)
3. [Vì sao chia module như hiện nay?](#3-vì-sao-chia-module-như-hiện-nay)
4. [Bốn tầng trong từng module](#4-bốn-tầng-trong-từng-module)
5. [Module giao tiếp như thế nào?](#5-module-giao-tiếp-như-thế-nào)
6. [Host khởi động và xử lý request](#6-host-khởi-động-và-xử-lý-request)
7. [Model và cơ sở dữ liệu Identity](#7-model-và-cơ-sở-dữ-liệu-identity)
8. [Vai trò, quyền và Admin](#8-vai-trò-quyền-và-admin)
9. [Hợp đồng API và ví dụ](#9-hợp-đồng-api-và-ví-dụ)
10. [Giải thích code bốn chức năng](#10-giải-thích-code-bốn-chức-năng)
11. [Bảo mật, giao dịch và giới hạn](#11-bảo-mật-giao-dịch-và-giới-hạn)
12. [Kiểm thử và Git workflow](#12-kiểm-thử-và-git-workflow)
13. [Chạy dự án và phát triển tiếp](#13-chạy-dự-án-và-phát-triển-tiếp)
14. [Hỏi đáp tổng hợp](#14-hỏi-đáp-tổng-hợp)
15. [Bản đồ đọc mã nguồn](#15-bản-đồ-đọc-mã-nguồn)

## 1. Hiện có gì, chưa có gì?

### 1.1. Dự án giải quyết việc gì?

Hệ thống quản lý một nhà hàng, phục vụ ăn tại bàn và mang về: thực đơn, bàn, gọi món, bếp/bar, giao từng phần, thu tiền, kho theo ngày bán và báo cáo. Đây là phạm vi sản phẩm, không phải toàn bộ đã có code.

Nguồn phạm vi ưu tiên: [MVP đã chốt](superpowers/specs/2026-11-05-restaurant-mvp-scope.md). Khi tài liệu cũ mâu thuẫn phạm vi này, không tự đưa chức năng đã bỏ trở lại.

### 1.2. Bảng trạng thái thực tế

| Thành phần | Hiện trạng | Bằng chứng |
|---|---|---|
| Solution backend | Một API Host, 40 project module, 2 project test | [Solution](../RestaurantManagement.slnx), [kiểm thử kiến trúc](../tests/Restaurant.ArchitectureTests/DependencyTests.cs) |
| 10 module, mỗi module 4 tầng | Đã có khung và đăng ký DI/route group | [ModuleRegistration](../src/Host/Restaurant.Api/Modules/ModuleRegistration.cs) |
| Identity | Đã có model, 2 migration, seed vai trò/quyền, bootstrap Admin | [IdentityDbContext](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Persistence/IdentityDbContext.cs) |
| Login/me/logout/change-password | Đã có API và test tích hợp | [IdentityEndpoints](../src/Modules/Identity/Restaurant.Modules.Identity.Presentation/IdentityEndpoints.cs) |
| Chín module còn lại | _(11/11)_ SalesDay đã có hai endpoint và test khóa ứng dụng; Catalog đã có migration, chưa có API; **bảy module còn lại** (Seating, Sales, Fulfillment, Billing, Inventory, Reporting, Audit) mới có DbContext/schema/route trống | Ví dụ [SeatingEndpoints](../src/Modules/Seating/Restaurant.Modules.Seating.Presentation/SeatingEndpoints.cs) |
| Phân quyền vận hành | Đã có danh mục/ánh xạ và claim; chưa có handler nghiệp vụ hoàn chỉnh | [IdentityCatalog](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Persistence/IdentityCatalog.cs) |
| Quản lý tạo tài khoản cấp dưới | Đã có mã quyền dự kiến; chưa có endpoint tạo/gán quyền | Đối chiếu catalog và bốn route Identity |
| Frontend, QR, âm thanh | _(11/11)_ Đã có khung `web/` (Next.js 16, chưa đăng nhập thật); QR và âm thanh vẫn chưa làm và vẫn bắt buộc theo MVP | [MVP](superpowers/specs/2026-11-05-restaurant-mvp-scope.md) |
| SignalR, Hangfire, R2, Nginx/Compose | Thành phần dự kiến, chưa có tích hợp tương ứng trong code base này | [README](../README.md), [package hiện dùng](../Directory.Packages.props) |
| Cơ sở dữ liệu thật | Không có bằng chứng migration đã áp dụng hoặc Admin thật đã tạo | Host không tự migrate/seed khi chạy |

**Điểm cần tránh hiểu nhầm:** có module Billing không có nghĩa đã thu tiền được; có bảng Permission không có nghĩa mọi thao tác đã được kiểm tra quyền; CI publish artifact không có nghĩa đã deploy sản phẩm.

### 1.3. Những gì giữ lại và đã giảm tải

Giữ khách gọi món qua QR trong phiên bàn đang mở, nhân viên xác nhận trước khi chuyển bếp/bar; thông báo âm thanh kèm hiển thị dự phòng; chế biến/giao từng phần; tiền mặt/chuyển khoản xác nhận thủ công; nhiều lần thu; miễn thu/hoàn theo số tiền; kho và giá vốn theo ngày bán.

Không thuộc MVP: chấm công, ca nhân viên/lương, công thức và trừ kho từng món, giữ chỗ kho, offline, thẻ, một khoản thanh toán cho nhiều hóa đơn, hoàn theo món được chọn, mở lại ngày đã chốt, phân tích nâng cao và báo món quá hạn tự động. **Ngày bán không phải ca chấm công.**

Nguồn: [Keep/Defer trong MVP](superpowers/specs/2026-11-05-restaurant-mvp-scope.md).

## 2. Công nghệ và kiến trúc

### 2.1. Công nghệ đang có trong source

| Công nghệ | Để làm gì? | Nguồn |
|---|---|---|
| C#, .NET 10; SDK 10.0.300, rollForward latestPatch | Ngôn ngữ/runtime/toolchain backend | [global.json](../global.json), [Directory.Build.props](../Directory.Build.props) |
| ASP.NET Core Minimal API | Host, route, middleware, DI, authentication/authorization | [Program.cs](../src/Host/Restaurant.Api/Program.cs) |
| EF Core SQL Server 10.0.8 | ORM, mapping, migration, transaction | [Directory.Packages.props](../Directory.Packages.props) |
| ASP.NET Core Identity EF 10.0.8 | User/role, băm mật khẩu, password policy, lockout | [IdentityModule](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/IdentityModule.cs) |
| JwtBearer 10.0.8 | Kiểm tra JWT | [AuthenticationConfiguration](../src/Host/Restaurant.Api/Configuration/AuthenticationConfiguration.cs) |
| SQL Server 2022 | Baseline database và SQL Server Docker cho test | [CI](../.github/workflows/backend-ci.yml) |
| OpenAPI 10.0.8 | Xuất hợp đồng JSON khi Development; chưa có Swagger UI | [Program.cs](../src/Host/Restaurant.Api/Program.cs) |
| xUnit, MVC Testing, Test SDK | Kiểm tra project references và API trên test host | [tests](../tests) |
| GitHub Actions | Restore, format, build, test, publish artifact và test SQL riêng | [workflow](../.github/workflows/backend-ci.yml) |

Package quản lý tập trung tại Directory.Packages.props; packages.lock.json khóa dependency graph. Nullable bật, warnings thành errors, deterministic build bật. Các phiên bản ở đây là phiên bản của source tại mốc đối chiếu, không phải tuyên bố phiên bản mới nhất ngoài thị trường.

### 2.2. Công nghệ mới ở mức định hướng

README dự kiến Next.js 16/React 19/TypeScript, Tailwind/shadcn, TanStack Query, Zustand, React Hook Form/Zod; backend dự kiến FluentValidation, SignalR, Hangfire, Serilog; triển khai dự kiến VPS + Docker Compose + Nginx; tệp dự kiến Cloudflare R2. Hiện không được mô tả chúng là đã cài hoặc hoạt động.

Cloudflare DNS/proxy/WAF là lớp mạng/bảo mật triển khai, **khác Cloudflare R2** là lưu trữ đối tượng. Hội thoại có yêu cầu dùng bảo mật Cloudflare, nhưng repo hiện không chứng minh gói dịch vụ, rule hoặc tài khoản Cloudflare đã cấu hình. Không suy diễn có WAF chỉ từ việc README ghi R2. Khi triển khai cần chốt DNS/proxy, TLS, rule và cách khóa origin.

Refresh token xuất hiện trong định hướng cũ; bản Identity hiện tại chỉ có JWT 15 phút và phiên SQL, **không có refresh endpoint**.

### 2.3. Vì sao Modular Monolith?

Một backend chạy trong một đơn vị triển khai, nhưng chia quyền sở hữu nghiệp vụ theo module. Lợi ích thiết kế: ít chi phí vận hành hơn nhiều service độc lập, gọi nội bộ không bắt buộc HTTP, vẫn giữ ranh giới code/data để tránh mọi bảng và logic dồn vào một nơi.

Đây không phải microservices: các csproj không phải các service có cổng riêng. Tách project là ranh giới biên dịch, không phải tách tiến trình. Source đăng ký tất cả module trong cùng Host: [ModuleRegistration.cs](../src/Host/Restaurant.Api/Modules/ModuleRegistration.cs).

### 2.4. Sơ đồ đúng với phần đã triển khai


```text
HTTP client
    |
    v
ASP.NET Core Host / middleware / route
    |
    v
Identity Presentation -> IIdentityAuthentication
                               |
                         DI resolution
                               |
                               v
                     IdentityAuthentication
                        |               |
                        v               v
                  UserManager      IdentityDbContext
                                        |
                                        v
                              SQL Server / identity

IAccessTokenIssuer -> JwtAccessTokenIssuer (Host)
Chín module khác: đã đăng ký nhưng chưa có use case nhà hàng.
```

Ở hình trên, DI resolution là chọn implementation khi chạy, không phải Application tham chiếu project Infrastructure. Chưa vẽ Redis, broker hoặc frontend như thành phần đã hoạt động vì chưa có chúng.

## 3. Vì sao chia module như hiện nay?

### 3.1. Module khác model ở đâu?

- **Module** là một phần nghiệp vụ có ranh giới: Identity, Billing, Catalog...
- **Model/entity** là kiểu dữ liệu hoặc thực thể: Permission, LoginSession, IdentityUser.
- **DTO** là hình dạng trao đổi API: LoginRequest, CurrentAccount, LoginResult.
- **DbContext** là đơn vị mapping/tracking/truy cập dữ liệu của module, không phải một database server riêng.

Do đó gọi “chia model” khi nói đến 10 phần hệ thống nên hiểu chính xác là **chia module**. Một module có thể sở hữu nhiều model và DTO.

### 3.2. Mười module và lý do tách

| Module / schema | Sở hữu dự kiến | Vì sao không nhập vào module khác? | Trạng thái |
|---|---|---|---|
| Identity / identity | Tài khoản, vai trò, quyền, phiên | Danh tính dùng xuyên hệ thống; không trộn mật khẩu vào Order | Đã có chức năng |
| Seating / seating | Khu vực, bàn, phiên bàn, chuyển bàn | Vòng đời phiên bàn khác vòng đời đơn | Khung |
| Catalog / catalog | Món, biến thể, tùy chọn, giá, cờ hết | Giá hiện tại khác giá đã chốt trên đơn | Khung |
| Sales / sales | Order, đợt gọi, phần món, giá chốt | Ghi nhận yêu cầu bán, không điều khiển mọi lần chế biến/thu tiền | Khung |
| Fulfillment / fulfillment | Bếp/bar, chế biến, giao, sự cố, làm lại | Một phần món có thể làm lại mà không tạo doanh thu mới | Khung |
| Billing / billing | Nghĩa vụ tiền, thu, miễn thu, hoàn | Giao món và thu tiền có vòng đời khác nhau | Khung |
| Inventory / inventory | Nguyên liệu, đơn vị, nhập, đếm tồn, giá vốn | Không có trừ kho theo món trong MVP; tính theo ngày | Khung |
| SalesDay / salesday | Mở/chốt ngày bán, đối soát | Chốt cần kiểm tra nhiều module, không phải lịch làm nhân viên | Khung |
| Audit / audit | Lịch sử thao tác quan trọng | Lịch sử nghiệp vụ khác log lỗi kỹ thuật | Khung |
| Reporting / reporting | Mô hình đọc/tổng hợp | Tối ưu truy vấn báo cáo không nên làm loãng quy tắc ghi | Khung |

Nguồn danh sách đang đăng ký: [ModuleRegistration](../src/Host/Restaurant.Api/Modules/ModuleRegistration.cs). Phạm vi sở hữu: [README](../README.md). Chi tiết dữ liệu tương lai: [đề xuất ERD](superpowers/specs/2026-11-05-database-revised-design.md), hiện ghi rõ REVIEW PROPOSAL.

### 3.3. Vì sao 10 nhóm chức năng không trùng 10 module?

Nhóm chức năng phục vụ phân tích người dùng; module phục vụ ranh giới kỹ thuật. Chế biến và phục vụ cùng nằm Fulfillment, còn “quản trị hệ thống” liên quan Identity và Audit. Không cần tạo một module riêng cho mỗi oval use case hoặc mỗi menu giao diện.

## 4. Bốn tầng trong từng module

### 4.1. Hướng phụ thuộc biên dịch

```text
Host -> Presentation -> Application -> Domain
Host -> Infrastructure -> Application
                       -> Domain
```

[DependencyTests.cs](../tests/Restaurant.ArchitectureTests/DependencyTests.cs) kiểm tra chính xác danh sách ProjectReference của 10 x 4 project, và cấm PackageReference/FrameworkReference trong Domain/Application. Không phải chỉ là sơ đồ trong báo cáo.

| Tầng | Trách nhiệm | Ví dụ Identity |
|---|---|---|
| Domain | Kiểu dữ liệu/quy tắc cốt lõi độc lập framework | Permission, RolePermission, LoginSession |
| Application | Hợp đồng use case, DTO, validation thuần | IIdentityAuthentication, IAccessTokenIssuer, AuthenticationInput |
| Infrastructure | Identity/EF/SQL, hiện thực port | IdentityAuthentication, IdentityDbContext, AdminBootstrapper |
| Presentation | Route, nhận DTO, ánh xạ HTTP response | IdentityEndpoints |
| Host, ở ngoài module | Lắp ghép DI, JWT/middleware/OpenAPI | Program, AuthenticationConfiguration, JwtAccessTokenIssuer |

### 4.2. Tại sao endpoint không gọi DbContext?

Endpoint chỉ biết IIdentityAuthentication trong Application. DI ánh xạ sang IdentityAuthentication ở Infrastructure; vì vậy xử lý HTTP không cần biết mapping bảng hoặc câu SQL khóa.

Dẫn chứng: [IdentityEndpoints.cs:15](../src/Modules/Identity/Restaurant.Modules.Identity.Presentation/IdentityEndpoints.cs#L15), [IdentityModule.cs](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/IdentityModule.cs).

### 4.3. Tại sao implementation use case lại ở Infrastructure?

Trong lát cắt Identity hiện tại, thao tác gắn chặt UserManager, EF và SQL transaction nên implementation nằm Infrastructure; Application giữ port framework-free. Đây là lựa chọn thực tế của source, không nên nói Application đã chứa tất cả orchestration. Khi nghiệp vụ phức tạp tăng, có thể đưa orchestration thuần vào Application qua các port phù hợp; không cần refactor trước khi có nhu cầu.

### 4.4. Tại sao không tự viết User/Role trong Domain?

Source dùng IdentityUser<Guid> và IdentityRole<Guid> từ framework tại Infrastructure để tận dụng store/hash/password policy/lockout. Không đưa framework vào Domain chỉ để mọi entity “cùng một tầng”. Domain hiện có entity đơn giản, chưa phải mô hình DDD đầy đủ.

### 4.5. DI, scoped và singleton dùng ở đâu?

IdentityAuthentication và DbContext đăng ký scoped, dùng trong scope request; TimeProvider.System được TryAddSingleton; token issuer là singleton đọc IOptions<JwtSettings>. DI cho phép thay dependency trong test. Không tự new DbContext ở endpoint và không dùng DbContext scoped như singleton.

Nguồn: [IdentityModule](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/IdentityModule.cs), [AuthenticationConfiguration](../src/Host/Restaurant.Api/Configuration/AuthenticationConfiguration.cs).

## 5. Module giao tiếp như thế nào?

### 5.1. Hiện tại đã có gọi liên module chưa?

**Chưa.** Host gọi các hàm đăng ký module không phải giao tiếp nghiệp vụ giữa Sales và Billing. Các module khác chưa có entity/use case; hiện chưa có Contracts project, event bus, Outbox, Inbox hoặc worker xử lý liên module.

Nguồn: [backend-foundation.md](backend-foundation.md), [DependencyTests](../tests/Restaurant.ArchitectureTests/DependencyTests.cs).

### 5.2. Thiết kế dự kiến là gì?

Khi cần, module cung cấp sở hữu contract nhỏ gồm interface/DTO/event thuần; module khác trao đổi ID và snapshot cần thiết. Phải cập nhật test kiến trúc khi bổ sung reference Contracts. Không tham chiếu Domain/Infrastructure, DbSet hoặc navigation EF của module khác.

Ví dụ **minh họa thiết kế, chưa có implementation**:

```text
Sales xác nhận đợt gọi
  -> hỏi Catalog qua contract: biến thể hợp lệ, còn bán, giá
  -> hỏi SalesDay/Seating: ngày và phiên hợp lệ
  -> ghi đợt/phần món và nghĩa vụ tiền qua contract Billing
  -> commit nghiệp vụ
  -> phát sự kiện đáng tin cậy
  -> Fulfillment tạo công việc bếp/bar
  -> SignalR thông báo UI; UI phát âm thanh nếu được phép
```

Những thao tác tài chính cần nguyên tử không được coi là an toàn chỉ vì “sau này có event”. Đề xuất dữ liệu yêu cầu shared transaction qua module APIs hoặc trạng thái pending bền vững cho các điểm quan trọng. Lựa chọn chi tiết chưa được coi đã phê duyệt chỉ từ ví dụ trên.

### 5.3. Một database, 10 schema có ý nghĩa gì?

Mỗi module có DbContext và cấu hình bảng lịch sử migration trong schema riêng, cùng connection string Restaurant. Schema không tự biến thành database hay rào bảo mật nếu SQL login vẫn có quyền rộng.

Không FK vật lý xuyên module là **định hướng**, không phải bảo toàn tham chiếu tự động. ID tham chiếu liên module cần contract kiểm tra, transaction khi cần, reconciliation và test. Trong module Identity, FK vật lý vẫn được dùng.

Dẫn chứng: [IdentityPersistence](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Persistence/IdentityPersistence.cs), [SeatingPersistence](../src/Modules/Seating/Restaurant.Modules.Seating.Infrastructure/Persistence/SeatingPersistence.cs), [ERD đề xuất](superpowers/specs/2026-11-05-database-revised-design.md).

### 5.4. Hai DbContext cùng chuỗi kết nối đã cùng transaction chưa?

Chưa. Cần chia sẻ DbConnection/DbTransaction và chủ động enlist các context. Hai context tự mở hai connection không tự rollback cùng nhau. Host hiện chưa có coordinator chốt ngày hay shared transaction liên module.

### 5.5. Outbox và âm thanh sẽ ở đâu?

Outbox dự kiến lưu sự kiện cùng transaction dữ liệu; worker phát sau commit; consumer cần deduplicate/Inbox để không áp dụng lặp. SignalR là kênh đưa thông tin tới UI, không phải nguồn chân lý tài chính. Âm thanh là phản hồi UI: phải có mute, user activation, fallback hiển thị, không phát lại lịch sử khi reconnect. Toàn bộ phần này thuộc kế hoạch, chưa có bảng/Hub/client hiện tại.

## 6. Host khởi động và xử lý request

### 6.1. Khởi động theo thứ tự nào?

[Program.cs](../src/Host/Restaurant.Api/Program.cs):

1. Nếu có --bootstrap-admin, chạy lệnh riêng rồi return, không khởi động HTTP server.
2. Tạo builder; đăng ký ProblemDetails, exception handler, OpenAPI, authentication và tất cả module.
3. Nạp CORS allowlist; build app.
4. Bật exception/status pages; ngoài Development/Testing bật HSTS và HTTPS redirect.
5. CORS; middleware giới hạn/cache Identity; authentication; authorization; rate limiter.
6. Map health, OpenAPI theo môi trường, system/modules, các module; Run.

Options JWT và connection string được ValidateOnStart. Chưa có secret/config cần thiết thì chạy bình thường có thể fail startup; không nên chữa bằng hardcode khóa vào source.

### 6.2. Authentication khác authorization ra sao?

Authentication xác minh danh tính/token/phiên. Authorization quyết định danh tính đó được làm gì. Fallback policy yêu cầu đăng nhập; login/health/OpenAPI Development được AllowAnonymous rõ ràng. SystemAdministration yêu cầu role Admin.

Trong request có token: kiểm tra chữ ký/issuer/audience/lifetime trước, rồi kiểm tra phiên SQL và lấy quyền hiện tại trước authorization. Vì thế DB không khả dụng thì endpoint bảo vệ không thể dựa riêng JWT để tiếp tục cho qua.

### 6.3. Có những endpoint nền nào?

| Route | Quyền/môi trường | Ý nghĩa |
|---|---|---|
| GET /health/live | Anonymous | Host đang sống, không chạy SQL check |
| GET /health/ready | Anonymous | Kiểm tra SQL connectivity; không chứng minh migration đầy đủ |
| GET /openapi/v1.json | Chỉ Development, anonymous | Hợp đồng API JSON |
| GET /api/system/modules | Admin | Danh sách tên module đăng ký; không phải danh sách tính năng đã hoàn thành |

Nguồn: [Program](../src/Host/Restaurant.Api/Program.cs), [SqlServerHealthCheck](../src/Host/Restaurant.Api/Configuration/SqlServerHealthCheck.cs).

### 6.4. Vì sao không mở CORS mọi nơi?

Appsettings mặc định AllowedOrigins rỗng; origin cần được khai báo khi tích hợp frontend. CORS là hạn chế phía trình duyệt, không thay xác thực, không ngăn mọi client gọi API và không thay firewall.

## 7. Model và cơ sở dữ liệu Identity

### 7.1. Các bảng thực tế

Có 11 entity table trong model Identity; bảng __EFMigrationsHistory là hạ tầng EF, không tính vào 11 bảng nghiệp vụ/kỹ thuật sau:

| Bảng | Vai trò | Khóa/liên kết chính |
|---|---|---|
| AspNetUsers | Username/email, hash, stamp, lockout, IsActive | PK Id Guid |
| AspNetRoles | Vai trò | PK Id Guid |
| AspNetUserRoles | Gán nhiều vai trò cho user | PK ghép UserId, RoleId; FK user/role |
| AspNetUserClaims | Claim tùy chỉnh của user | PK Id; FK UserId |
| AspNetUserLogins | Hạ tầng đăng nhập ngoài của Identity | PK LoginProvider, ProviderKey; FK UserId |
| AspNetUserTokens | Hạ tầng token provider của Identity | PK UserId, LoginProvider, Name |
| AspNetRoleClaims | Claim tùy chỉnh của role | PK Id; FK RoleId |
| Permission | Danh mục quyền nguyên tử | PK Code, varchar tối đa 100; Name bắt buộc |
| RolePermission | Vai trò có những quyền gì | PK RoleId + PermissionCode; FK cả hai |
| AdminBootstrap | Ghi nhận lần bootstrap đầu | PK Id cố định 1, UserId unique + FK |
| LoginSession | Phiên cấp JWT, hạn dùng/thu hồi/stamp | PK Id Guid, FK UserId, rowversion |

Nguồn: [IdentityDbContext](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Persistence/IdentityDbContext.cs), [migration đầu](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Persistence/Migrations/20261105063121_InitialIdentity.cs), [migration phiên](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Persistence/Migrations/20261105073827_IdentityLoginSessions.cs).

Các bảng UserClaims/UserLogins/UserTokens/RoleClaims xuất hiện vì kế thừa model Identity chuẩn; **không có nghĩa** đã triển khai Google login, email reset password, MFA hoặc tự động sử dụng mọi claim trong đó. Quyền hiện lấy từ RolePermission, không có gán quyền trực tiếp cho từng user trong lát cắt này.

### 7.2. Quan hệ và lực lượng liên kết

```text
User 1 ---- 0..N UserRole N..0 ---- 1 Role
Role 1 ---- 0..N RolePermission N..0 ---- 1 Permission
User 1 ---- 0..N LoginSession
User 1 ---- 0..1 AdminBootstrap
Toàn bảng AdminBootstrap: tối đa 1 dòng (CHECK Id = 1).
```

User-Role và Role-Permission là nhiều-nhiều qua bảng nối; khóa ghép ngăn gán trùng. Session thuộc đúng một user, user có nhiều phiên. Marker bootstrap là dấu vết khởi tạo, không phải bảng chứa mọi Admin.

FK custom LoginSession/RolePermission/AdminBootstrap dùng Restrict. Không khái quát rằng toàn bộ FK framework đều Restrict: phải xem migration Identity chuẩn; một số FK Identity có cascade.

### 7.3. Tại sao Guid cho Identity?

Framework store hiện dùng IdentityUser<Guid>/IdentityRole<Guid>; giữ thống nhất khóa user/role và tham chiếu tương ứng tránh đổi schema/framework giữa chừng. Không chuyển tất cả user ID thành bigint theo ERD cũ. Đề xuất entity nhà hàng dùng bigint không thay đổi quyết định Guid của Identity.

### 7.4. Vì sao DTO không dùng thẳng entity?

LoginRequest chỉ nhận username/password; CurrentAccount chỉ trả id/username/email/roles/permissions. Không serialize IdentityUser ra API vì dễ lộ PasswordHash, SecurityStamp, ConcurrencyStamp và chi tiết nội bộ.

Dẫn chứng: [AuthenticationContracts.cs](../src/Modules/Identity/Restaurant.Modules.Identity.Application/Authentication/AuthenticationContracts.cs).

### 7.5. IsActive nằm đâu nếu IdentityUser không có property?

Đây là EF shadow property bool được cấu hình trong IdentityDbContext; truy vấn dùng EF.Property<bool>(user, "IsActive"). SQL default true và HasSentinel(false) để instance mới nhận default true. Cần phân biệt tạo user mặc định active với cập nhật user hiện có sang false.

Giới hạn: không dựa cách cấu hình sentinel này để tạo ngay user disabled bằng một INSERT. API quản lý sau này cần thiết kế thao tác ấy rõ ràng và test, có thể đổi sang CLR property/model thích hợp. Test hiện đã kiểm tra vô hiệu hóa user hiện có chặn login/token.

### 7.6. LoginSession có những thuộc tính gì?

Id/UserId xác định phiên/chủ sở hữu; SecurityStamp gắn với trạng thái bảo mật tài khoản; CreatedAt/ExpiresAt dùng UTC; RevokedAt null nghĩa chưa thu hồi; RowVersion là token đồng thời ở model. CHECK ExpiresAt > CreatedAt; index theo UserId+RevokedAt và ExpiresAt.

Không lưu JWT thô, password hoặc refresh token. RowVersion **không tự bảo vệ tất cả thao tác**: ExecuteUpdate hiện không kiểm tra expected RowVersion; các mutation xác thực đang dùng transaction và SQL application lock theo user.

### 7.7. Migration nào đã có và vì sao không sửa migration cũ?

InitialIdentity tạo Identity/catalog quyền/bootstrap. IdentityLoginSessions bổ sung IsActive/LoginSession. Migration mới additive giữ lịch sử thay vì sửa âm thầm migration đã chia sẻ. ModelSnapshot mô tả model gần nhất, không phải script chạy trực tiếp.

Có file migration không nghĩa DB thật đã cập nhật. Host không gọi Migrate/EnsureCreated khi startup. Trước triển khai phải review SQL, backup, apply migration đúng môi trường rồi bootstrap.

## 8. Vai trò, quyền và Admin

### 8.1. Năm vai trò đã seed là gì?

| Mã role | Tên hiển thị nghiệp vụ | Phạm vi seed điển hình |
|---|---|---|
| Admin | Quản trị viên | Quản lý tài khoản mọi cấp, vai trò/quyền |
| Manager | Quản lý | Cấp dưới, thực đơn, điều hành, tiền/kho/ngày bán/báo cáo |
| Staff | Nhân viên | Phiên bàn, đơn, xác nhận đợt, giao, thu tiền, đề nghị hoàn |
| Kitchen | Bếp | Xử lý trạm bếp, báo sự cố, cờ hết/nhập đếm theo quyền |
| Bar | Bar | Xử lý trạm bar, báo sự cố, cờ hết/nhập đếm theo quyền |

Tên dùng trong JWT/DB là Admin/Manager/Staff/Kitchen/Bar, không gửi role “Quản lý” thay Manager. Một user có thể có nhiều role. **Không có role Customer** trong năm role nội bộ.

### 8.2. Danh mục 26 quyền và seed cho ai?

| Permission | Role |
|---|---|
| identity.accounts.manage | Admin |
| identity.roles.manage | Admin |
| identity.subordinates.manage | Manager |
| seating.catalog.manage | Manager |
| seating.sessions.manage | Manager, Staff |
| catalog.menu.manage | Manager |
| catalog.availability.manage | Manager, Staff, Kitchen, Bar |
| sales.orders.manage | Manager, Staff |
| sales.batches.confirm | Manager, Staff |
| fulfillment.kitchen.process | Kitchen |
| fulfillment.bar.process | Bar |
| fulfillment.incidents.report | Staff, Kitchen, Bar, Manager |
| fulfillment.incidents.approve | Manager |
| fulfillment.portions.serve | Manager, Staff |
| billing.payments.collect | Manager, Staff |
| billing.refunds.request | Manager, Staff |
| billing.refunds.approve | Manager |
| billing.refunds.pay | Manager |
| billing.waivers.approve | Manager |
| inventory.catalog.manage | Manager |
| inventory.purchases.manage | Manager |
| inventory.counts.enter | Manager, Staff, Kitchen, Bar |
| inventory.counts.approve | Manager |
| inventory.adjustments.approve | Manager |
| salesday.manage | Manager |
| reporting.read | Manager |

Nguồn trực tiếp: [IdentityCatalog.cs](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Persistence/IdentityCatalog.cs). Đây là seed baseline, không bảo đảm DB thực tế chưa bị sửa. API lấy assignments hiện tại trong DB, không giả định seed luôn đúng.

### 8.3. Admin có mọi quyền không? Manager có quyền bếp không?

Không tự động. Seed Admin chỉ có hai quyền quản trị, Manager không có fulfillment.kitchen.process hoặc fulfillment.bar.process. Nếu nghiệp vụ muốn kiêm nhiệm thì gán thêm role/quyền theo API tương lai và kiểm tra phạm vi trạm. Không có quan hệ kế thừa role trong code hiện tại; mũi tên actor use case không tạo kế thừa authorization tự động.

### 8.4. Quản lý đã tạo tài khoản cấp dưới được chưa?

Chưa. Có permission identity.subordinates.manage nhưng chưa có endpoint. Khi làm cần chặn Manager tạo/nâng thành Admin hoặc Manager, chặn gán quyền vượt cấp, kiểm tra role đích phía backend và test. Không suy ra rule đã chạy từ tên permission.

### 8.5. Admin đầu tiên tạo thế nào và ở đâu?

Lệnh --bootstrap-admin chạy trước web startup. Đọc BootstrapAdmin__UserName/Email/Password và connection từ environment; không nhận password qua command argument. UserManager kiểm tra policy/băm password, transaction + application lock bảo vệ đồng thời; từ chối nếu marker/Admin đã tồn tại; không nâng quyền user trùng username/email. Tạo user, gán Admin, ghi marker rồi commit cùng giao dịch.

Nguồn: [AdminBootstrapCommand](../src/Host/Restaurant.Api/Configuration/AdminBootstrapCommand.cs), [AdminBootstrapper](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Initialization/AdminBootstrapper.cs), [hướng dẫn khởi tạo](identity-initialization.md).

Không có mật khẩu mặc định trong repo; không có endpoint anonymous bootstrap. Chạy lại không reset mật khẩu. Sau khi chạy phải gỡ secret bootstrap khỏi môi trường; đây không phải tính năng reset Admin khi mất tài khoản.

## 9. Hợp đồng API và ví dụ

Nguồn chính xác: [IdentityEndpoints](../src/Modules/Identity/Restaurant.Modules.Identity.Presentation/IdentityEndpoints.cs), [DTO](../src/Modules/Identity/Restaurant.Modules.Identity.Application/Authentication/AuthenticationContracts.cs).

| Method / route | Cần Bearer? | Input | Thành công | Lỗi chính |
|---|---|---|---|---|
| POST /api/identity/login | Không | userName, password | 200 LoginResult | 400, 401, 413, 429 |
| GET /api/identity/me | Có | Không body | 200 CurrentAccount | 401 |
| POST /api/identity/logout | Có | Không body | 204 | 401 |
| POST /api/identity/change-password | Có | currentPassword, newPassword | 204 | 400, 401, 413, 429 |

Lỗi hạ tầng bất ngờ có thể 500. Không có route register/refresh/forgot-password/reset-password/accounts trong lát cắt này. GET /me không nhận userId để người gọi tùy ý xem tài khoản khác.

### 9.1. Login mẫu

Các giá trị dưới đây là minh họa, không phải tài khoản/mật khẩu có sẵn.

```http
POST /api/identity/login
Content-Type: application/json

{"userName":"nhanvien01","password":"<mat-khau-that-duoc-cap>"}
```

```json
{
  "accessToken": "<jwt-tra-ve>",
  "expiresAt": "2026-11-05T09:15:00Z",
  "account": {
    "id": "11111111-1111-4111-8111-111111111111",
    "userName": "nhanvien01",
    "email": "nhanvien01@example.invalid",
    "roles": ["Staff"],
    "permissions": ["sales.orders.manage", "sales.batches.confirm"]
  }
}
```

Danh sách permissions trên đã rút gọn để minh họa; response thật là toàn bộ quyền hiện có, distinct và sort. Không login bằng email: service gọi FindByNameAsync.

### 9.2. Đọc tài khoản và logout

```http
GET /api/identity/me
Authorization: Bearer <jwt-tra-ve>

POST /api/identity/logout
Authorization: Bearer <jwt-tra-ve>
```

/me trả CurrentAccount, không bọc trong account như LoginResult. Logout 204 không có body; dùng lại phiên đó bị 401 ở lần kiểm tra kế tiếp. Gọi logout lần hai bằng token đã thu hồi sẽ bị 401, không được hứa luôn 204.

### 9.3. Đổi mật khẩu

```http
POST /api/identity/change-password
Authorization: Bearer <jwt-tra-ve>
Content-Type: application/json

{"currentPassword":"<mat-khau-cu>","newPassword":"<mat-khau-moi-hop-le>"}
```

Thành công 204; người dùng phải login lại, kể cả thiết bị vừa đổi. Không trả JWT mới. Sai mật khẩu cũ/mật khẩu mới yếu/trùng mật khẩu cũ trả lỗi 400, không tự thu hồi phiên hợp lệ.

### 9.4. Validation ở đâu?

AuthenticationInput kiểm tra username không trắng, tối đa 256, không có khoảng trắng đầu/cuối; password không trắng và tối đa 1024. Không Trim password. Điều kiện này là kiểm tra hình dạng đầu vào, **không phải** password policy.

Mật khẩu mới/khởi tạo do UserManager kiểm tra policy: tối thiểu 12 ký tự, chữ hoa/thường, số và ký tự không chữ-số theo cấu hình/default Identity hiện dùng. Login không ép password cũ phải thỏa policy mới vì phải kiểm tra hash đã lưu.

## 10. Giải thích code bốn chức năng

### 10.1. Login hoạt động thế nào?

Nguồn: [IdentityAuthentication.cs:19](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Authentication/IdentityAuthentication.cs#L19).

1. Kiểm tra AuthenticationInput. Endpoint chặn input xấu thành 400.
2. UserManager tìm username. Không tồn tại thì kiểm tra dummy hash rồi trả thất bại chung.
3. Mở transaction, lấy SQL application lock theo user, Reload user để không dùng trạng thái trước khi chờ khóa.
4. Từ chối user biến mất, inactive, MFA enabled hoặc đang lockout. MFA chưa triển khai nên fail closed.
5. CheckPasswordAsync; sai thì AccessFailedAsync và commit bộ đếm; đủ 5 lần sai có thể lock tài khoản theo policy.
6. Đúng thì reset bộ đếm khi cần, bảo đảm SecurityStamp tồn tại, đọc roles/permissions hiện tại.
7. Tạo session Guid, stamp, now UTC, hết hạn sau 15 phút; gọi token issuer.
8. Save session và commit; chỉ sau đó mới trả token/account.

Nếu persist/commit lỗi, không trả token đăng nhập thành công. Có dummy hash giảm khác biệt đường xử lý nhưng **không bảo đảm constant-time tuyệt đối** giữa mọi trường hợp.

### 10.2. JWT chứa gì và được kiểm tra ở đâu?

[JwtAccessTokenIssuer](../src/Host/Restaurant.Api/Configuration/JwtAccessTokenIssuer.cs) cấp JWT HS256 với sub=userId, sid=sessionId, jti ngẫu nhiên, iat, role snapshot, issuer/audience và thời hạn. JWT được ký, **không mã hóa**, không nhét mật khẩu vào payload.

[AuthenticationConfiguration.cs](../src/Host/Restaurant.Api/Configuration/AuthenticationConfiguration.cs) kiểm tra issuer, audience, lifetime, signed token, signing key, thuật toán HS256; ClockSkew 30 giây. Tuy nhiên session SQL vẫn có hạn cứng theo giờ server; skew JWT không kéo dài session trong DB.

OnTokenValidated yêu cầu đúng một sub và sid, parse Guid; service kiểm tra session SQL. Chữ ký đúng nhưng sid giả, session thuộc user khác, session hết hạn hoặc bị thu hồi vẫn bị từ chối.

### 10.3. /me lấy dữ liệu từ token hay DB?

[FindSessionUserAsync:104](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Authentication/IdentityAuthentication.cs#L104) kiểm tra session/user/stamp/active/lockout; [GetAccountAsync:119](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Authentication/IdentityAuthentication.cs#L119) join UserRoles/Roles/RolePermission.

Middleware thay role/permission claims cũ bằng DB hiện tại và cất CurrentAccount vào HttpContext.Items[typeof(CurrentAccount)]. Endpoint /me trả đối tượng này, không thực hiện lại riêng một lần truy vấn account ở handler. Không có cache quyền dài hạn trong triển khai này.

Vì vậy đổi role trong DB có hiệu lực ở lần xác thực request kế tiếp. UserClaims/RoleClaims không được hợp nhất tự động vào permissions bởi đoạn code này.

### 10.4. Logout thu hồi thế nào?

[LogoutAsync:74](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Authentication/IdentityAuthentication.cs#L74) mở transaction, khóa user, kiểm tra lại phiên, ExecuteUpdate RevokedAt cho đúng user/session chưa thu hồi, rồi commit. Không xóa mọi session và không đổi mật khẩu.

JWT vẫn có thể đúng chữ ký sau logout, nhưng session DB không hợp lệ nên request sau bị chặn. Xóa token ở trình duyệt chỉ là việc client, không thay thao tác server này.

### 10.5. Đổi mật khẩu bảo vệ nhiều thiết bị thế nào?

[ChangePasswordAsync:86](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Authentication/IdentityAuthentication.cs#L86):

1. Chặn input xấu hoặc password mới giống cũ.
2. Mở transaction và khóa theo user.
3. Đọc lại session và user với tracking, tránh lỗi UserManager dùng nhiều instance cùng khóa.
4. UserManager.ChangePasswordAsync kiểm tra password cũ, policy mới và thay hash/security stamp.
5. Cập nhật RevokedAt cho tất cả session còn hiệu lực của user.
6. Commit cùng giao dịch; trả Success -> HTTP 204.

Lỗi sau khi ghi password nhưng trước commit phải rollback cả password và thu hồi session. Có test chủ động chèn lỗi để xác minh điều này, không chỉ kiểm tra đường thành công.

### 10.6. Tại sao phải khóa cả login và đổi mật khẩu?

Nếu không điều phối, login có thể lấy stamp cũ hoặc tạo phiên giữa lúc đổi password. [LockUserAsync:133](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Authentication/IdentityAuthentication.cs#L133) dùng sp_getapplock Exclusive, LockOwner=Transaction, timeout 15 giây, resource riêng từng user. Login/logout/change-password của cùng user được nối tiếp trong SQL; user khác không dùng cùng resource.

Đây là khóa SQL, khác lock trong bộ nhớ một process. Nếu tương lai API quản trị thay password/disable hoặc quản lý phiên, cần dùng cùng protocol khi cần; không được cho rằng mọi tác vụ tương lai đã tự đồng bộ.

## 11. Bảo mật, giao dịch và giới hạn

### 11.1. Những lớp hiện có

- Bearer JWT + DB session validation; fallback authenticated; policy Admin cho route hệ thống.
- Hash/policy/lockout qua Identity; năm lần sai, thời gian mặc định Identity hiện tại là năm phút.
- Rate login 10/phút/IP kết nối trực tiếp; password 5/phút/user; fixed window, queue 0, trả 429.
- Identity response no-store/no-cache; request body tối đa 16 KiB tại middleware/Kestrel feature khi có thể chỉnh.
- DTO chứa credential/token override ToString thành redacted; không bật request-body logging cho chúng.
- Exception handler trả ProblemDetails chung, có traceId; server vẫn log exception để vận hành.
- Secret trống trong appsettings, đọc từ cấu hình môi trường/user secrets phù hợp.

Nguồn: [Program](../src/Host/Restaurant.Api/Program.cs), [AuthenticationConfiguration](../src/Host/Restaurant.Api/Configuration/AuthenticationConfiguration.cs), [GlobalExceptionHandler](../src/Host/Restaurant.Api/Configuration/GlobalExceptionHandler.cs), [DTO](../src/Modules/Identity/Restaurant.Modules.Identity.Application/Authentication/AuthenticationContracts.cs).

### 11.2. Giới hạn quan trọng trước triển khai thật

| Giới hạn | Hệ quả / việc cần làm |
|---|---|
| Rate limit trong process | Nhiều replica không chung bộ đếm; cần edge/distributed policy khi triển khai |
| Chưa trusted forwarded headers | Qua Nginx/Cloudflare có thể chung IP proxy; không tin X-Forwarded-For tùy ý |
| Chưa refresh token | Hết 15 phút phải đăng nhập lại; không giả định phiên làm việc cả ngày |
| Session query mỗi request | Thu hồi quyền nhanh hơn nhưng phụ thuộc SQL và tăng tải; đo tải/index thực tế |
| Request đã xác thực đang chạy | Logout không hủy ngược request đó; hành động nhạy cảm tương lai cần check trong transaction |
| Chưa nghiệp vụ permission handler | Có claim không thay thế kiểm tra action, trạm và phạm vi dữ liệu |
| Chưa frontend/token storage policy | Không chốt localStorage/cookie từ code backend hiện tại; cần threat model frontend/CSRF/XSS |
| Chưa MFA/reset password/email confirm flow | Không hứa các tính năng ấy hoạt động; MFA-enabled user bị từ chối |
| Chưa job dọn session | Cần retention/cleanup theo vận hành, không dùng vô hạn dữ liệu phiên |
| Chưa triển khai key rotation/audit auth chuyên dụng | Cần quyết định khi đưa ra production; logger kỹ thuật không phải Audit nghiệp vụ |

GlobalExceptionHandler hiện log exception; không được tuyên bố hệ thống tuyệt đối không thể ghi dữ liệu nhạy cảm ở mọi log. Cần review sink, retention và cấu hình EF/request logging khi deploy; không bật sensitive-data logging.

### 11.3. Chống lỗi đồng thời đã có đến đâu?

Đã có transaction + user app lock cho mutation xác thực, singleton marker + khóa bootstrap, FK/check/index Identity và kiểm thử rollback. Chưa có idempotency/outbox/locking cho tiền, kho, chốt ngày vì chưa implement các use case ấy. Giữ ranh giới này để không lấy bảo đảm của login áp sang mọi giao dịch nhà hàng.

## 12. Kiểm thử và Git workflow

### 12.1. Bằng chứng kiểm chứng hiện có

Ở phiên triển khai commit 2bca5f6: **64 test đạt, 0 fail, 0 skip** khi có SQL Server Docker tách biệt: 62 integration + 2 architecture. Sau đó CI Build and test và Identity SQL Server tests đều thành công trên push/PR. Đây là kết quả đã ghi nhận trước, không phải tài liệu này vừa chạy lại toàn suite.

Tệp tham khảo: [identity-authentication.md](identity-authentication.md), [kế hoạch có kết quả](superpowers/plans/2026-11-05-identity-authentication.md), [workflow](../.github/workflows/backend-ci.yml).

### 12.2. Test chứng minh những gì?

| Rủi ro | Test/bằng chứng |
|---|---|
| Module reference sai tầng | Every_module_has_four_isolated_projects; No_module_uses_shared_build_files_to_bypass_reference_rules trong [DependencyTests](../tests/Restaurant.ArchitectureTests/DependencyTests.cs) |
| Logout nhầm mọi thiết bị | Login_me_and_logout_revoke_only_current_session_without_leaking_secrets |
| Password change không thu hồi toàn bộ | Password_change_revokes_all_devices_and_requires_new_password |
| Password sai/yếu làm mất phiên | Wrong_current_password_or_weak_new_password_keeps_existing_sessions |
| Lockout và rò lỗi user | Five_wrong_passwords_lock_account_and_unknown_user_has_same_error |
| Tài khoản disabled vẫn dùng token | Disabled_account_cannot_login_or_use_an_existing_token |
| Claim role cũ tiếp tục có quyền | Current_roles_replace_stale_token_roles |
| Hai lần đổi password cùng thành công | Concurrent_password_changes_cannot_both_succeed |
| Ghi password xong lỗi gây trạng thái dở dang | Password_write_failure_rolls_back_password_and_session_revocation |
| JWT ký đúng vượt qua kiểm tra phiên | Valid_signature_cannot_bypass_expiry_session_ownership_or_session_existence |

Các test xác thực trên ở [IdentitySqlServerAuthenticationTests.cs](../tests/Restaurant.IntegrationTests/IdentitySqlServerAuthenticationTests.cs). Bootstrap/migration có sáu SQL test riêng tại [IdentitySqlServerTests.cs](../tests/Restaurant.IntegrationTests/IdentitySqlServerTests.cs), gồm chống nâng quyền user có sẵn, bootstrap đồng thời, password yếu và rollback.

### 12.3. Tại sao cần SQL thật thay vì chỉ mock?

Mock không xác minh sp_getapplock, relational constraints, transaction rollback, ExecuteUpdate hay hành vi EF/Identity tracking trên SQL Server. SQL tests tạo DB tên ngẫu nhiên và dọn sau test; không trỏ vào DB nhà hàng.

SqlServerFact sẽ skip nếu thiếu IDENTITY_TEST_SQLSERVER. Vì thế dotnet test thông thường không chứng minh test SQL đã chạy; cần xem số skip và chạy job SQL riêng. CI có job Start disposable SQL Server và filter FullyQualifiedName~IdentitySqlServer, bao gồm hai nhóm SQL test.

### 12.4. Git workflow đang áp dụng

[CONTRIBUTING.md](../CONTRIBUTING.md): nhánh feature/fix/docs -> PR develop -> PR main khi ổn định. Không force-push, không tự merge PR, không commit mật khẩu. CI không tự deploy/migrate DB thật. Workflow file không tự bật branch protection trên GitHub; settings cần được cấu hình riêng.

Mốc source: login trên feature/identity-authentication commit 2bca5f6; parent Identity đã có trong develop qua PR #7 tại nền local 92d1520. Không khẳng định develop đã có login khi PR #8 chưa được xác nhận merge. Tài liệu mới này không tự tạo commit/push hoặc thay đổi trạng thái PR.

## 13. Chạy dự án và phát triển tiếp

### 13.1. Chạy base theo thứ tự nào?

```powershell
dotnet restore RestaurantManagement.slnx --locked-mode
dotnet build RestaurantManagement.slnx -c Release --no-restore
dotnet test RestaurantManagement.slnx -c Release --no-build --no-restore
dotnet format whitespace RestaurantManagement.slnx --verify-no-changes --no-restore
```

Sau đó cấu hình ConnectionStrings__Restaurant, Jwt__Issuer, Jwt__Audience, Jwt__SigningKey bằng secret store hoặc environment. Signing key cần ít nhất 32 UTF-8 bytes theo JwtSettings. Không điền secret vào ví dụ tài liệu, source hoặc terminal history chia sẻ.

Review/apply SQL migration theo [identity-initialization.md](identity-initialization.md), bootstrap Admin có chủ đích, gỡ biến bootstrap rồi chạy:

```powershell
dotnet run --project src/Host/Restaurant.Api --launch-profile http --no-restore
```

Launch profile HTTP hiện dùng localhost:5080; không bảo đảm port luôn trống hoặc là URL production. /health/live có thể đạt dù chưa có DB sẵn sàng; kiểm tra thêm /health/ready và login thực tế sau migration.

### 13.2. Nên thêm chức năng vào đâu?

Ví dụ tạo tài khoản cấp dưới: DTO/interface ở Identity.Application; policy cấp dưới, UserManager/persistence/transaction ở các port/implementation phù hợp; route tại Identity.Presentation; DI tại IdentityModule; test HTTP + SQL + chống nâng quyền. Không viết toàn bộ nghiệp vụ trong Program.cs.

Ví dụ thực đơn: entity Catalog.Domain, hợp đồng Catalog.Application, EF mapping/migration và implementation Catalog.Infrastructure, HTTP Catalog.Presentation; không đưa MenuItem vào Identity chỉ vì Identity đã có DB chạy.

### 13.3. Những bước kế tiếp hợp lý

1. Review/merge PR xác thực theo workflow; kiểm thử cấu hình/migration/admin ở môi trường phát triển riêng.
2. Quản trị tài khoản cấp dưới và permission authorization, đặc biệt chống vượt cấp và giới hạn trạm.
3. Catalog, Seating và mở ngày bán trước luồng tạo/xác nhận order.
4. Order -> Fulfillment -> serving; tích hợp realtime/âm thanh rồi QR theo kế hoạch MVP.
5. Billing, Inventory và chốt ngày sau khi chốt các hợp đồng tài chính/kho/transaction còn mở.
6. Frontend, edge/proxy/TLS, storage, backup/restore và kiểm thử đầu-cuối theo từng lát cắt.

Đây là hướng phát triển, không tuyên bố đã xong hoặc bắt buộc phải thêm refresh token ngay trước mọi chức năng khác.

## 14. Hỏi đáp tổng hợp

### Tại sao không có một AppDbContext cho mọi bảng?

Mỗi module sở hữu mapping/migration để tránh thay đổi một nghiệp vụ kéo toàn bộ data model vào nhau. Đổi lại transaction và tham chiếu liên module phải điều phối có chủ đích. Bằng chứng: mỗi Persistence có DbContext riêng và migration history schema riêng.

### Tại sao không gọi HTTP giữa các module?

Hiện chúng ở cùng backend, chưa có lý do bắt buộc thêm network hop. Giao tiếp dự kiến qua contract nội bộ; nếu sau này tách service thì cần thiết kế consistency/network failure riêng, không chỉ đổi interface thành URL.

### Quyền người dùng ở đâu?

Assignments ở AspNetUserRoles và RolePermission; danh mục ở Permission; middleware dựng claim từ DB cho request. CurrentAccount trả bản nhìn hiện tại, không phải nơi lưu dữ liệu gốc. Xem GetAccountAsync và OnTokenValidated.

### Khách QR có phải đăng ký tài khoản không?

MVP định hướng guest grant theo phiên/order, không public registration và không role Customer trong seed. Quyền guest QR phải tách khỏi tài khoản nhân viên. Chưa có endpoint/table guest grant thực thi trong Identity hiện tại; xem MVP và ERD proposal, không suy ra QR đã dùng JWT nội bộ.

### Tại sao bếp và bar là hai role nhưng không hai module?

Hai trạm chia quyền/phạm vi xử lý nhưng cùng vòng đời Fulfillment. Tách module theo nghiệp vụ, không theo mỗi người dùng. Permission riêng kitchen.process/bar.process không thay kiểm tra ticket đúng trạm ở use case tương lai.

### Tại sao hoàn thành món không trừ kho?

MVP bỏ recipe/per-dish inventory; kho tính theo nhập/đếm/ngày bán. Không tự thêm reserve/deduct vào Fulfillment. Đây là quy tắc phạm vi, chưa phải luồng Inventory đã code.

### Tại sao token còn hạn mà vẫn bị 401?

Vì phải đồng thời hợp lệ chữ ký và phiên DB: có thể session bị thu hồi, user disabled/locked, stamp thay đổi, sid sai/chủ sở hữu khác hoặc SQL session đã hết hạn. Xem FindSessionUserAsync; không chỉ xem exp trong JWT.

### /me khác tra tài khoản bất kỳ ở đâu?

/me dùng sub/sid đã được xác thực, không cho truyền userId. API quản trị xem user khác chưa có. Endpoint không trả hash/stamp.

### Logout có xóa mật khẩu hoặc dữ liệu user không?

Không. Chỉ đánh dấu RevokedAt của phiên hiện tại. Đổi mật khẩu thay hash/stamp và thu hồi tất cả phiên, vẫn không xóa user.

### Tại sao dùng transaction dù UserManager đã SaveChanges?

UserManager có thể ghi từng bước, nhưng bootstrap hoặc đổi password gồm nhiều thay đổi phải thành công/rollback cùng nhau. Transaction bao ngoài gom các ghi cùng DbContext/connection; test fault injection kiểm chứng rollback.

### ConcurrencyStamp, SecurityStamp và RowVersion khác nhau thế nào?

SecurityStamp dùng kiểm tra trạng thái bảo mật/phiên; ConcurrencyStamp của Identity hỗ trợ phát hiện cập nhật cạnh tranh; RowVersion là SQL token trên session. Chúng không thay nhau, và không tự thay protocol khóa ứng dụng.

### Có cần generic repository cho mọi bảng ngay không?

Chưa. Source dùng DbContext/UserManager trong Infrastructure; port Application che chi tiết framework khỏi endpoint. Thêm abstraction khi có use case thật, không tạo lớp trung gian chỉ để số tầng trông nhiều hơn. Xem backend-foundation.md.

### OpenAPI có phải màn hình đăng nhập không?

Không. Hiện chỉ xuất JSON Development; chưa có Swagger UI hoặc frontend nhà hàng. Có thể gọi API bằng HTTP client sau khi cấu hình DB/admin đúng.

### Chạy CI thành công có bảo đảm sản phẩm production-ready không?

Không. Nó chứng minh các check đã chạy trên commit tương ứng; chưa chứng minh tải thật, cấu hình Cloudflare/proxy/TLS, backup restore, toàn bộ nghiệp vụ tiền/kho hoặc frontend. Cần acceptance và kiểm chứng triển khai riêng.

### ERD trong repo có phải database đã triển khai toàn bộ không?

Không. database-revised-design.md ghi REVIEW PROPOSAL, còn các gate C01-C06 và quy tắc cần phê duyệt. Migration thực tế hiện chỉ Identity. Không sinh migration tiền/kho từ proposal mà bỏ qua các điểm chưa chốt.

### Tại sao README đôi chỗ nói chưa có migration/token endpoint?

Một số đoạn nền còn mô tả thời điểm trước Identity. Bản này ưu tiên kiểm chứng endpoint/migration source tại commit đã ghi, không lấy câu cũ phủ nhận code hiện có. Không sửa lịch sử tài liệu để che khác biệt; cần cập nhật đồng bộ tài liệu nền trong lần chỉnh riêng.

## 15. Bản đồ đọc mã nguồn

Đọc theo thứ tự dưới đây để nối kiến trúc với chức năng:

| Thứ tự | Tệp | Câu hỏi được trả lời |
|---|---|---|
| 1 | [Directory.Build.props](../Directory.Build.props), [Directory.Packages.props](../Directory.Packages.props), [global.json](../global.json) | Build bằng gì, package nào, quy tắc chất lượng? |
| 2 | [Program.cs](../src/Host/Restaurant.Api/Program.cs) | Startup, middleware, route chung ở đâu? |
| 3 | [ModuleRegistration.cs](../src/Host/Restaurant.Api/Modules/ModuleRegistration.cs) | 10 module lắp vào Host thế nào? |
| 4 | [DependencyTests.cs](../tests/Restaurant.ArchitectureTests/DependencyTests.cs) | Ranh giới project được kiểm tra ra sao? |
| 5 | [IdentityEndpoints.cs](../src/Modules/Identity/Restaurant.Modules.Identity.Presentation/IdentityEndpoints.cs) | HTTP gọi port nào, trả status gì? |
| 6 | [AuthenticationContracts.cs](../src/Modules/Identity/Restaurant.Modules.Identity.Application/Authentication/AuthenticationContracts.cs) | DTO/interface/validation thuần là gì? |
| 7 | [IdentityModule.cs](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/IdentityModule.cs) | DI, policy password và store đăng ký thế nào? |
| 8 | [IdentityAuthentication.cs](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Authentication/IdentityAuthentication.cs) | Login/validate/logout/change-password thực hiện ở đâu? |
| 9 | [JwtAccessTokenIssuer.cs](../src/Host/Restaurant.Api/Configuration/JwtAccessTokenIssuer.cs), [AuthenticationConfiguration.cs](../src/Host/Restaurant.Api/Configuration/AuthenticationConfiguration.cs) | Cấp token và kiểm tra request hoạt động thế nào? |
| 10 | [IdentityDbContext.cs](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Persistence/IdentityDbContext.cs), [IdentityCatalog.cs](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Persistence/IdentityCatalog.cs) | Model, khóa, quan hệ, seed nằm đâu? |
| 11 | [AdminBootstrapper.cs](../src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Initialization/AdminBootstrapper.cs) | Admin đầu tiên được tạo an toàn ra sao? |
| 12 | [SQL authentication tests](../tests/Restaurant.IntegrationTests/IdentitySqlServerAuthenticationTests.cs), [SQL bootstrap tests](../tests/Restaurant.IntegrationTests/IdentitySqlServerTests.cs) | Bằng chứng happy path, lỗi và cạnh tranh? |
| 13 | [Backend CI](../.github/workflows/backend-ci.yml), [CONTRIBUTING](../CONTRIBUTING.md) | Check trước merge và quy trình Git? |

### Kết luận tại mốc đối chiếu

Đây là **nền backend Modular Monolith có Identity đã hoạt động ở mức API**, không còn là bộ thư mục trống, nhưng cũng chưa là hệ thống nhà hàng hoàn chỉnh. Giá trị đã có: ranh giới tầng/module, cấu hình build/CI, database Identity, seed quyền, Admin bootstrap, bốn chức năng xác thực kèm phiên SQL và test thực. Phần tiếp theo phải xây theo từng lát cắt nghiệp vụ, giữ QR/âm thanh và phạm vi đã giảm tải; không đánh đồng sơ đồ/ERD/permission seed với implementation.
