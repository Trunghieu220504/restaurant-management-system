# ADR-01: Contracts và nền tảng dùng chung

- Ngày: 12/11/2026. Chủ trì: Hiếu. Task: HIE-1.2.
- Trạng thái: **Đề xuất, chờ Phương và Hải review. Không phải quyết định đã được nhóm duyệt.**
- Mốc phân tích ban đầu: develop 17244f5; nhánh tài liệu tách từ develop 1333e67.
- PR này chỉ chứa ADR đề xuất; không kèm code HIE-1.2 hoặc tài liệu hướng dẫn.

## Bối cảnh và vấn đề

Mỗi module đang có Domain/Application/Infrastructure/Presentation. DependencyTests
kiểm tra đúng các phụ thuộc nội module và không cho gọi module khác. SalesDay đã
có OpenDayRequest, BusinessDayResponse, IBusinessDays trong Application, API trả
ProblemDetails kèm code và lưu RequestKey trong ngày bán. BusinessDays dùng khóa
Exclusive `restaurant:salesday:open` để bảo vệ điều kiện chỉ một ngày hoạt động.
Spike C03 chứng minh hai DbContext có thể dùng chung SQL transaction, nhưng chưa
có helper production cho khóa Shared/Exclusive theo ngày. Chưa có Outbox worker.

Nền tảng mới cần cho các module tuần 2 dùng chung hợp đồng mà không chia sẻ entity
EF, DbContext hoặc Infrastructure. Không chuyển business behavior sang một lớp
generic làm mất quyền sở hữu module.

## Phương án và đề xuất

1. Giữ tất cả trong Application: ít thay đổi nhưng module tiêu thụ phải tham chiếu
   nội bộ của module khác, trái quy tắc Contracts đã định trong kế hoạch.
2. Tạo Contracts thuần theo module, nền tảng kỹ thuật nhỏ dùng chung: đề xuất chọn
   vì giữ ownership và có thể kiểm tra phụ thuộc bằng test.
3. Shared project chứa mọi DTO/entity/service: không chọn vì làm mất ranh giới.

## 1. Ranh giới project

- Bốn tầng hiện hữu giữ nguyên. Tạo `Restaurant.Modules.<X>.Contracts` khi thực sự
  xuất interface/DTO cho module khác; không tạo 10 project rỗng.
- Contracts không có PackageReference, FrameworkReference hoặc ProjectReference;
  chỉ dùng BCL, immutable DTO và CancellationToken. Không entity EF, IQueryable,
  DbConnection hay HttpContext trong hợp đồng.
- Domain giữ không tham chiếu project khác. Application được tham chiếu Domain
  của chính mình và Contracts được liệt kê rõ trong allowlist của test kiến trúc.
- Infrastructure/Presentation giữ phụ thuộc cũ; nếu cần Contracts phải khai báo
  rõ và có test. Không cho phép wildcard tham chiếu Infrastructure module khác.
- Shared primitives kỹ thuật đặt ngoài Modules. Không sử dụng Directory.Build.props
  để tự động chèn project references. Mọi ngoại lệ đi cùng test và lý do.

HIE-1.2 chưa chuyển DTO SalesDay hoặc đổi namespace đang được frontend dùng.
Contracts đầu tiên chỉ nên được nối vào khi có consumer cụ thể hoặc adapter được
Phương review. Test kiến trúc cần hỗ trợ Contracts tùy chọn, không buộc mọi module
phải có project thứ năm.

## 2. Lỗi và HTTP

- API giữ `/api/<module>`, JSON camelCase; tiền VND integer; timestamp UTC.
- Lỗi nghiệp vụ trả ProblemDetails với extension `code` ổn định, không parse title.
- Mã theo `<module>.<subject>.<reason>`. Giữ nguyên toàn bộ `salesday.*` hiện có.
- 400: dữ liệu/key thiếu hoặc sai; 401: chưa xác thực; 403: thiếu quyền;
  409: xung đột trạng thái/idempotency; lỗi hạ tầng không lộ SQL/token/stack trace.
- Không đổi hàng loạt lỗi Identity trong task này; đó là thay đổi hợp đồng riêng.
- Primitive lỗi chỉ chứa code/message, HTTP mapping ở Presentation/Host.

## 3. Idempotency

- Idempotency-Key là Guid khác Guid.Empty, bắt buộc ở thao tác tài chính/chuyển
  trạng thái theo hợp đồng endpoint. Validate trước khi ghi dữ liệu.
- Cùng ý định, key, actor và nội dung chuẩn hóa: trả kết quả đã commit. Khác nội
  dung hoặc actor cho cùng key: 409. Quy tắc chuẩn hóa do endpoint xác định.
- Key và hiệu ứng nghiệp vụ phải cùng transaction. Không lưu key vào memory cache
  rồi coi là bảo đảm chống lặp bền vững.
- SalesDay giữ RequestKey, actor/date/openingCash comparison, response 200 cho
  replay và 201 cho lần đầu. Không thêm bảng idempotency generic trong HIE-1.2.
- Chính sách giữ dữ liệu/replay của payment sẽ được xác định với Billing; không
  tự xóa key cũ theo TTL chung. Retry do lỗi mạng giữ nguyên key, đổi ý định dùng key mới.

## 4. Khóa ngày và giao dịch C03

- Writer theo ngày dùng Shared; chốt ngày dùng Exclusive. Resource thống nhất dự
  kiến `restaurant:salesday:<Guid N lowercase>`; phải được Phương xác nhận trước
  khi các module nghiệp vụ sử dụng cùng nhau.
- Khóa dùng LockOwner=Transaction trong database ứng dụng, không Session.
- Helper nhận connection và transaction đã mở từ caller; kiểm tra transaction
  thuộc connection đó. Không tự mở transaction, commit hoặc dispose tài nguyên caller.
- Validate dayId, mode, timeout hữu hạn trước SQL. Parameterize resource/mode/timeout.
- Return code >=0 mới là thành công; -1 là timeout; các mã âm khác thất bại. Không
  thực thi nghiệp vụ khi không lấy được khóa. Cancellation được truyền xuống SQL.
- Sau khi có khóa, đọc lại trạng thái ngày trong transaction; chỉ DANG_BAN cho ghi.
  Helper khóa không tự chứng minh điều kiện nghiệp vụ này.
- Thứ tự khóa: ngày → order/phiên → hóa đơn → thanh toán → kho. Nhiều ngày thì
  lấy theo thứ tự ID thống nhất. Retry deadlock phải retry cả transaction/idempotency.
- Giữ riêng global opening lock `restaurant:salesday:open`; helper theo ngày không
  thay thế được điều kiện mở ngày mới vì lúc đó chưa có dayId để khóa.
- Không thêm FK vật lý chéo schema hoặc chia sẻ EF entities. Coordinator được chỉ
  định rõ chịu trách nhiệm connection/transaction chung; Contracts nghiệp vụ không
  expose transaction. Thiết kế coordinator cụ thể cần use case trước khi triển khai.

## 5. Outbox abstraction

- Envelope tối thiểu: EventId (Guid), OccurredAt (UTC), Version (positive integer),
  Type ổn định và payload có kiểu do producer sở hữu. Không dùng tên CLR assembly
  làm hợp đồng wire; schema version thay đổi được review.
- Abstraction biểu diễn việc enqueue trong unit of work hiện tại. Enqueue không
  có nghĩa đã publish; không gửi SignalR/email hoặc gọi network trong transaction.
- Adapter persistence sau này phải ghi sự kiện cùng transaction dữ liệu nghiệp vụ.
  Nếu rollback thì không được có sự kiện để phát.
- Worker phát sau commit; giả định at-least-once, consumer khử trùng bằng EventId.
  Không tuyên bố exactly-once. Retry/backoff/poison messages, retention và multiple
  instances là tiêu chí cho task worker riêng do Hiếu/Hải phối hợp.
- HIE-1.2 chỉ cung cấp abstraction và validation envelope. Không thêm broker,
  migration Outbox, worker hay endpoint giả để chứng minh hệ thống đã hoạt động.

## 6. Tương thích và rollout

Bản triển khai thử chưa commit ở nhánh cục bộ `feature/shared-foundation-hie-1-2` tạo `src/Shared/Restaurant.Shared.Contracts`
(BusinessError, IdempotencyKeys, IntegrationEvent và IOutboxWriter) và
`src/Shared/Restaurant.Shared.SqlServer` (BusinessDayLock). Cả hai chỉ dùng BCL;
helper nhận DbConnection/DbTransaction nhưng SQL thực thi dành riêng SQL Server.
Chưa module runtime nào tham chiếu hai project này; chỉ bộ test tiêu thụ chúng.
Chưa tạo module Contracts rỗng. Test kiến trúc kiểm tra purity khi Contracts xuất
hiện, nhưng allowlist của module hiện vẫn giữ nguyên: mọi tham chiếu mới phải
được bổ sung minh bạch cùng consumer và review. Đây là giới hạn cố ý của bản đề xuất,
không phải bằng chứng rằng liên module/Outbox production đã được tích hợp.

Thay đổi additive: không sửa API, migration đã merge hoặc dữ liệu vận hành.
Không bật runtime behavior mới cho SalesDay khi chưa có adapter/consumer và review.
OpenAPI/client không phải sinh lại nếu endpoint không đổi. Không thay quy tắc quyền
trong task này; RequirePermission thuộc HIE-1.4. Rollback bằng phiên bản API cũ nếu
chỉ thêm project/helper chưa được sử dụng; không có downgrade database.

## 7. Kiểm chứng bắt buộc

- Test kiến trúc: Contracts không kéo dependencies; module không tham chiếu nội bộ
  module khác; allowlist không chấp nhận project giả hoặc đường dẫn đánh tráo.
- Unit tests: key rỗng/sai, code lỗi và envelope sai version/ID/UTC bị từ chối.
- SQL helper: Shared cùng tồn tại, Exclusive bị chặn, khác ngày độc lập, commit và
  rollback giải phóng khóa; transaction/connection sai bị từ chối; timeout không
  cho ghi tiếp. Kết hợp hai DbContext rollback giữ test C03 hiện có.
- Regression: build, locked restore, format, toàn bộ backend test và SQL thật.
- Không suy ra staging đạt từ unit test; không tính test skip là đã nghiệm thu.

## 8. Review và câu hỏi cần trả lời

| Người | Nội dung | Trạng thái |
|---|---|---|
| Phương | Namespace khóa, thứ tự khóa, idempotency và tính nguyên tử C03 | Chờ review |
| Hải | CI, timeout, logging, rollout và phạm vi Outbox worker | Chờ review |
| Hiếu | Ranh giới Contracts, scope triển khai và test kiến trúc | Đề xuất |

Chưa có quyết định tự động chuyển sang Accepted. Link PR/review và SHA được duyệt
phải bổ sung khi có. Phương review nghiệp vụ/khóa/giao dịch; Hải review CI và vận hành.
Báo cáo chuẩn bị của Phương nằm ở PR #28, chưa thay thế review chính ADR này.

Các điểm cần chốt trước Accepted: khóa cố định hay theo ngày; isolation level và
đọc lại trạng thái sau khóa; không nâng Shared lên Exclusive khi chốt; ánh xạ
lỗi timeout tại API; replay SalesDay trả trạng thái hiện tại và không lưu yêu cầu
thất bại. Bản tài liệu này không tuyên bố toàn bộ test hoặc nghiệm thu đã đạt.
