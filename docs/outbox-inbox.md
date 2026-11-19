# Outbox và Inbox dùng chung (HIE-3.0)

Triển khai mục 5 của `docs/adr/ADR-01-shared-foundation.md`. Phát sự kiện giữa module theo
**at-least-once**: không có exactly-once; consumer phải khử trùng bằng `EventId`.

## Thành phần

| Thành phần | Vị trí | Vai trò |
|---|---|---|
| `IntegrationEvent<T>`, `IOutboxWriter` | `Shared.Contracts` | Phong bì sự kiện và lệnh xếp hàng trong unit of work hiện tại |
| `StoredIntegrationEvent`, `IIntegrationEventHandler`, `IntegrationEventJson` | `Shared.Contracts` | Sự kiện đã lưu (payload JSON), giao diện consumer, (de)serialize payload |
| Bảng `[schema].[Outbox]` + `OutboxEntry` + writer | Infrastructure của module sinh sự kiện (hiện: **Sales**) | Ghi sự kiện cùng `SaveChanges` với dữ liệu nghiệp vụ |
| `OutboxStore`, `OutboxDispatcher` | `Shared.SqlServer` | Thuê (lease) bản ghi đến hạn, phát, đánh dấu xong/lỗi |
| `InboxStore` + bảng `[schema].[Inbox]` | `Shared.SqlServer` + module nhận | Chặn xử lý lặp theo (Consumer, EventId) |
| `OutboxDispatcherService` | Host | `BackgroundService`, **tắt mặc định** (`Outbox:Enabled=true` để bật) |

`Shared.Contracts` và `Shared.SqlServer` vẫn không có Project/Package/FrameworkReference.
Infrastructure của module được phép tham chiếu **`Shared.Contracts`** (không phải
`Shared.SqlServer`) để cài `IOutboxWriter`; `DependencyTests` kiểm tra đúng điều đó.

## Chữ ký `IOutboxWriter`

```csharp
Task EnqueueAsync<TPayload>(IntegrationEvent<TPayload> message, string aggregateId, int aggregateVersion, CancellationToken ct)
```

Thêm `aggregateId` + `aggregateVersion` so với bản HIE-1.2 (chưa có consumer nào dùng bản cũ). Bộ ba
(`Type`, `AggregateId`, `AggregateVersion`) là duy nhất: xếp hàng cùng một sự thật hai lần sẽ bị CSDL từ
chối thay vì nhân đôi. `aggregateId` là chuỗi ASCII ≤ 64 ký tự (`a-z A-Z 0-9 - _ . :`).

## Mẫu dùng ở use case

```csharp
// Cùng DbContext, cùng SaveChanges: dữ liệu nghiệp vụ và sự kiện cùng commit hoặc cùng rollback.
batch.Confirm(...);
await outbox.EnqueueAsync(
    new IntegrationEvent<BatchConfirmed>(Guid.NewGuid(), clock.GetUtcNow(), "sales.batch_confirmed", 1, payload),
    aggregateId: batch.Id.ToString(), aggregateVersion: batch.SoLanCapNhat, ct);
await db.SaveChangesAsync(ct);   // nếu có transaction ngoài (khóa ngày), commit transaction đó sau
```

Không gọi mạng/SignalR trong transaction. Payload là kiểu do module sinh sự kiện sở hữu; consumer chọn kiểu
theo `Type` + `SchemaVersion`.

## Consumer và Inbox

```csharp
await using var tx = await connection.BeginTransactionAsync(ct);
if (!await InboxStore.TryRecordAsync(connection, tx, "fulfillment", "fulfillment.tickets", message.EventId, now, ct))
    return;                       // đã xử lý: bỏ qua
// ... tạo phiếu chế biến trên cùng connection/transaction ...
await tx.CommitAsync(ct);         // rollback thì dấu Inbox cũng mất, sự kiện sẽ được phát lại
```

Bảng Inbox có khóa chính (`Consumer`, `EventId`), cột `ProcessedAt datetime2`. Module nhận tự tạo bảng
bằng migration của mình (Fulfillment: `InitialFulfillment`, HIE-3.4).

## Hành vi dispatcher

- **Thuê:** `ClaimDueAsync` chọn tối đa `BatchSize` bản ghi đến hạn (`NextAttemptAt <= now`, chưa xử lý, không
  còn thuê) bằng `UPDLOCK, READPAST`, đặt `LeaseUntil` và tăng `Attempts`. Nhiều instance không lấy cùng một
  bản ghi cùng lúc; bản ghi của transaction chưa commit bị bỏ qua. Thuê hết hạn thì bản ghi đến hạn lại, nên
  instance chết giữa chừng không làm mất sự kiện (nhưng có thể phát lại: vì vậy cần Inbox).
- **Thành công:** `ProcessedAt` được đặt, hết thuê.
  Nếu lúc đánh dấu mà bản ghi đã được instance khác hoàn tất (thuê hết hạn khi đang phát), dispatcher gọi
  `completedElsewhere`; host ghi cảnh báo "hoàn tất bởi dispatcher khác". Thấy cảnh báo này nghĩa là `LeaseDuration`
  (mặc định 2 phút) ngắn hơn thời gian phát chậm nhất.
- **Nhiều handler cho một loại sự kiện:** chạy tuần tự; một handler lỗi thì cả bản ghi được thử lại và các handler
  trước đó chạy lại. Mỗi handler phải idempotent (Inbox theo consumer/event).
- **Lỗi:** thử lại với backoff nhân đôi (`BaseDelay` 5s, trần `MaxDelay` 15 phút). Đủ `MaxAttempts` (8) thì
  **park**: `NextAttemptAt = NULL`, giữ `LastError`, không tự phát lại. `RequeueParkedAsync` đưa chúng về hàng
  đợi sau khi người vận hành sửa nguyên nhân.
- **Tắt ứng dụng:** hủy giữa chừng không ghi lỗi; thuê hết hạn sẽ phát lại.
- **Thứ tự:** trong một lô, theo `OccurredAt`. Không đảm bảo thứ tự theo aggregate khi có retry; consumer không
  được phụ thuộc thứ tự, hãy dùng `AggregateVersion` nếu cần.
- Loại sự kiện chưa có handler được coi là **lỗi phát** (retry rồi park với lỗi "No handler is registered"),
  không bị bỏ qua âm thầm. Vì vậy chỉ bật `Outbox:Enabled` khi consumer tương ứng đã được triển khai; nếu lỡ bật
  sớm, dùng `RequeueParkedAsync` sau khi consumer sẵn sàng.

## Chưa làm (cố ý)

- Chỉ **Sales** có bảng Outbox. Seating chưa có sự kiện; Fulfillment nhận Outbox/Inbox cùng `InitialFulfillment`
  (HIE-3.4). Thêm module khác phải thêm bảng, writer riêng, và schema vào danh sách trong `OutboxDispatcherService`.
- Một DI container chỉ resolve được **một** `IOutboxWriter`. Khi module thứ hai cần ghi sự kiện, tách interface
  theo module (không đổi hợp đồng `Shared.Contracts`) hoặc dùng keyed service.
- Chưa có dọn dẹp bản ghi đã xử lý (retention), chưa có endpoint/giám sát bản ghi park, chưa kiểm thử tải.
- Chưa có handler thật: dispatcher tắt mặc định và chưa nên bật cho đến khi HIE-3.4/HIE-4.2 đăng ký consumer.
  `OutboxDispatcherService` chưa có test riêng (chỉ lõi `OutboxDispatcher` được kiểm thử trên SQL Server).
- Bằng chứng kiểm thử: `tests/Restaurant.IntegrationTests/OutboxInboxSqlServerTests.cs`, chạy trên SQL Server thật
  (cần `IDENTITY_TEST_SQLSERVER`; không có biến này test bị bỏ qua).
