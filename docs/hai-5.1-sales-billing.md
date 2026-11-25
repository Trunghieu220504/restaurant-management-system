# HAI-5.1 — xác nhận đợt, Billing và Fulfillment

Nhánh `fix/hai-5.1-sales-billing-atomicity` nối `IInvoiceCharging` vào bước xác nhận đợt. Trước sửa, một đợt có thể được xác nhận/phát Outbox nhưng chưa tạo hóa đơn, nên UI thu tiền không có nghĩa vụ để thu. Sau sửa, Sales, Billing và Outbox dùng cùng SQL transaction; lỗi bất kỳ phải rollback cả ba.

Test SQL thật tạo dữ liệu bằng bộ seed demo riêng, cố ý làm Outbox lỗi sau khi Billing ghi, xác nhận không còn hóa đơn/event nửa chừng; bỏ lỗi rồi xác nhận đồng thời, đúng một thành công và một `sales.batch.not_pending`; chỉ một dòng nghĩa vụ. Gửi lại cùng event cho consumer nhiều lần (kể cả đồng thời), chỉ một Inbox và một phiếu đầu. 31/31 test liên quan (Sales service + integration) đạt trên SQL Server 2022 Docker, không skipped.

Tiêu chí 5 có bằng chứng liên module thật; chưa thay UAT giao diện hoặc diễn tập trên staging. Việc nối realtime/UI cần được kiểm chứng tiếp trên bản demo tích hợp.
