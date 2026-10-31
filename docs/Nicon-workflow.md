# Quy trình vận hành NICON

Tài liệu này mô tả quy trình nghiệp vụ mục tiêu của NICON và ranh giới với phần
mềm hiện có. Baseline điều khiển là
`docs/Nicon/MoMs/2026-10-06/NICON_Meeting_2026-10-03_Tom-tat-yeu-cau-moi.md`;
giả định hoặc quyết định về sau chưa có xác nhận bằng văn bản không được ghi đè
baseline. Tài liệu cũng đối chiếu bộ tiến độ mẫu, BOQ mẫu và mã nguồn hiện tại.

## 1. Cách đọc trạng thái

- **Implemented:** có persistence, API authorization, UI và test phù hợp với
  phạm vi được nêu; không tự chứng minh đã triển khai hoặc nghiệm thu tại NICON.
- **Partial:** đã có lát cắt sử dụng được; phải nêu rõ phần còn thiếu.
- **Target:** yêu cầu mục tiêu chưa có đủ bằng chứng triển khai end-to-end.
- **Open decision:** còn thiếu quyết định hoặc đầu vào nghiệp vụ có thẩm quyền;
  phương án kỹ thuật không tự trở thành quy tắc đã nghiệm thu.

`Dự án` trong tài liệu này mặc định là **Operational Project** dùng chung. Các
`DesignProjectId` còn tồn tại trong Thiết kế, Pháp lý và một phần Thi công là
ranh giới tương thích của hệ thống hiện tại, không phải mô hình đích.

## 2. Trục dữ liệu xuyên suốt

```text
Khách hàng
  └─ Lead / Cơ hội
       └─ Operational Project (Dự án dùng chung)
            ├─ Báo giá / hồ sơ thầu
            ├─ Hợp đồng Upstream với CĐT/khách hàng
            ├─ Dự án Thiết kế và hồ sơ Pháp lý
            ├─ Thi công, QA/QC, HSE, QS và nghiệm thu
            ├─ BOQ thực hiện, RFQ, Hợp đồng Downstream và kho
            ├─ Thu/chi, hồ sơ thanh toán và P&L
            └─ Tài liệu, audit, dashboard và KPI
```

Các module dùng chung ID Dự án, không tạo lại dự án theo từng phòng ban. Team,
giao việc, hợp đồng, BOQ, tài liệu và bằng chứng KPI phải truy vết về cùng Dự án.

Theo quyết định của chủ sản phẩm trong review ngày 30/10/2026, một Operational
Project được có nhiều luồng Design; mỗi luồng có đội ngũ và tiến độ riêng.
Thành viên, phân công và tài liệu nguồn phải xác định đúng luồng; không suy
ra quyền truy cập luồng khác chỉ vì cùng Dự án vận hành. Finance trên Hợp đồng
đi về hồ sơ rà soát của Báo giá nguồn, không tạo vòng phê duyệt Hợp đồng riêng.
Danh sách chọn nhân sự phải hiển thị tên, vai trò và cho lọc theo phòng ban/nhóm
trong phạm vi ứng viên mà người dùng được quyền chọn.

### 2.1. Nhóm công việc của phòng ban — Hiện có

```text
Kinh doanh phụ trách Cơ hội / PM Dự án
  → chọn Dự án dùng chung và phòng nhận
  → chọn người chủ trì phòng
  → người chủ trì chia đầu việc cho nhân sự cùng phòng
  → người phụ trách cập nhật tiến độ
  → người xác nhận độc lập xác nhận mọi đầu việc Dự án và đầu việc có KPI
```

Nhóm theo Dự án không được tạo một Dự án khác cho phòng nhận. Danh sách tách
**Dự án** (chưa gắn hợp đồng) và **Hợp đồng** (đã gắn `ContractId`); tạo từ tab
Hợp đồng bắt buộc chọn hợp đồng cùng Dự án. Nhóm hiện có tự phân loại theo
`ContractId`; thêm hoặc gỡ hợp đồng chuyển nhóm giữa hai tab nhưng nhóm vẫn
thuộc Operational Project.
Các trạng thái nhóm là `Planned → Active → Completed`, hoặc `Cancelled` theo
chuyển trạng thái hợp lệ. Nhóm chỉ hoàn thành khi mọi đầu việc đã kết thúc và
mọi đầu việc Dự án đã được xác nhận. Quan hệ tiên quyết không được tạo vòng lặp;
đầu việc sau không được bắt đầu trước khi đầu việc trước hoàn tất.
Người chủ trì có thể thêm công việc con vào công việc cha chưa kết thúc trong
cùng nhóm việc. Tiến độ cha là trung bình tiến độ con đang hoạt động; trạng thái
cha chuyển theo con: chưa bắt đầu, đang làm, hoàn thành, rồi được xác nhận khi
mọi con đều được xác nhận. Với nhiều cấp, kết quả lan lên từng cấp cha. Chỉ
công việc lá cập nhật tiến độ và nhận xác nhận trực tiếp.

Công việc không theo hợp đồng dùng tab **Công việc nội bộ**, không mang project,
customer hoặc contract. Khi không chọn KPI, hoàn thành không cần bước xác nhận.
Khi chọn KPI, người xác nhận phải khác người thực hiện và sự kiện `Verified` mới
là bằng chứng chấm KPI.

**Implemented — mở lại và xóa nhóm việc:** Theo xác nhận của người dùng trong
phiên làm việc 23/10/2026, người có quyền quản lý và đúng phạm vi được mở nhóm
`Completed → Active` để bổ sung công việc; không thu hồi xác nhận KPI hoặc duyệt
tài chính cũ, không mở khóa riêng các đầu việc/hồ sơ đã chốt. Nhóm `Cancelled`
không mở lại. Xóa vĩnh viễn yêu cầu xem trước ảnh hưởng, nhập đúng mã nhóm và
kiểm tra phiên bản/kế hoạch trên server. Dữ liệu con thuộc nhóm bị xóa; bản ghi
cha và checklist độc lập được giữ, gỡ liên kết khi phù hợp. Hồ sơ đã sử dụng
bằng chứng giá/duyệt/kế hoạch, kích hoạt pháp lý và tệp chưa dọn vẫn chặn xóa.
Chi tiết API và kiểm thử tại `application_developer.md`, mục Department work
packages; thao tác người dùng tại `user_guide.md` §3.0.1.

### 2.2. Liên phòng ban trước hợp đồng — Hiện có

Ba luồng dưới đây dùng lại nhóm công việc dự án, không có tab riêng. Nhóm việc
tiền hợp đồng không được gắn `ContractId`.

#### 2.2.1. Cung ứng → Kinh doanh: báo giá đầu vào

```text
Kinh doanh (người phụ trách Cơ hội) hoặc PM Dự án
  → Kinh doanh tạo Báo giá nháp từ Cơ hội đã gắn Dự án, chỉ nhập thông tin cơ bản
  → giao nhóm việc "Giá đầu vào" cho phòng Cung ứng, gắn Báo giá nháp + Dự án + Cơ hội
  → Cung ứng nhập chào giá NCC/thầu phụ (Draft)
  → Cung ứng gửi duyệt (Submitted)
  → người duyệt khác người lập: Approved hoặc Rejected kèm lý do
  → Kinh doanh chọn giá Approved còn hiệu lực, nhập giá đầu ra Suất đầu tư/BOQ
  → gửi duyệt báo giá đầu ra khi đã có nội dung và tổng giá trị hợp lệ
```

Khi gửi duyệt Báo giá đầu ra, Kinh doanh chọn đúng một Trưởng phòng Cung ứng
đang hoạt động, có quyền xem và duyệt Báo giá, khác người gửi. Người gửi xem
trước email đã điền dữ liệu, gồm To và CC, rồi xác nhận; hệ thống chuyển Báo giá
sang `PendingApproval` và xếp email vào hàng gửi trong cùng lần lưu. CC gồm
thành viên đang hoạt động của Dự án vận hành gắn Báo giá mà tại thời điểm gửi
vẫn có quyền xem chính Báo giá đó; không gửi giá trị báo giá cho thành viên
thiếu quyền. Chỉ Trưởng phòng Cung ứng được chọn có thể duyệt hoặc từ chối
trên hệ thống. Quy trình duyệt nội bộ có một bước; email là thông báo, không
tự phê duyệt. Màn hình Báo giá hiển thị người duyệt và kết quả gửi email; nếu
kết quả chưa xác định, người dùng liên hệ quản trị viên kiểm tra trước khi gửi
lại. Quy tắc người duyệt được chốt ngày 29/10/2026 và đổi theo ghi chú
30/10/2026, với xác nhận bổ sung ngày 31/10/2026 về role và phạm vi CC.

Chỉ sửa được chào giá ở `Draft`/`Rejected`; chào giá đã bị báo giá tham chiếu thì
khoá lại. Báo giá lưu bản chụp giá đầu vào tại thời điểm gắn. Giá hết hiệu lực,
chưa duyệt, khác Dự án hoặc khác nhóm việc Cung ứng đều bị từ chối.
Một nhóm việc giá đầu vào có thể nhận nhiều chào giá nhà cung cấp, mỗi giá có
trạng thái duyệt riêng. Từ chi tiết Báo giá, yêu cầu đã gắn được mở lại thay vì
tạo thêm nhóm trùng; màn Báo giá hiển thị từng giá và trạng thái của chúng.
Trạng thái đóng/mở nhóm việc không thay thế trạng thái duyệt của từng chào giá.

Kiểm soát **BOQ đã ký** của Cung ứng là bước **sau** hợp đồng và tách biệt: chỉ
BOQ thực hiện gắn hợp đồng đầu ra đã ký, còn hiệu lực, đúng Dự án mới được lập.

#### 2.2.2. Tài chính – Kế toán: rà soát sau báo giá và trước khi nộp thầu

```text
Kinh doanh gửi báo giá (đã duyệt nội bộ)
  → Tài chính lập hồ sơ rà soát điều kiện hợp đồng / bảo lãnh / điều khoản thanh toán
  → Tài chính gửi duyệt, người duyệt độc lập phê duyệt
  → Kinh doanh gửi báo giá cho khách hàng
  → trước khi ký Hợp đồng Upstream gắn Báo giá nguồn, dùng lại hồ sơ Tài chính
    đã duyệt cho đúng phiên bản Báo giá; không lập hồ sơ hợp đồng riêng

Kinh doanh chuẩn bị hồ sơ dự thầu (gói thầu đã gắn Dự án vận hành)
  → lập Kế hoạch đấu thầu (`TenderPlanning`) theo từng phòng: người phụ trách, người duyệt độc lập,
    ngày bắt đầu/kết thúc, dependency và tiến độ
  → từng phòng hoàn thành đầu việc; người duyệt xác nhận
  → Tài chính lập hồ sơ rà soát tài chính dự thầu / bảo lãnh
  → Tài chính gửi duyệt, người duyệt độc lập phê duyệt
  → Kinh doanh nộp thầu (Submitted); hệ thống chốt bất biến đúng revision bằng
    chứng, dự toán, kế hoạch, rà soát Tài chính, kênh/biên nhận, người và thời điểm nộp
  → nếu bên mời thầu yêu cầu bổ sung, lập revision bằng chứng và lần nộp mới;
    không ghi đè snapshot hoặc thời điểm nộp đầu tiên
  → ghi nhận trúng/trượt; nếu trúng, mở Cơ hội Negotiation trên đúng Dự án
  → sau thương thảo, người dùng chủ động tạo Hợp đồng Upstream
```

Theo xác nhận người dùng ngày 25/10/2026, khi gửi duyệt báo giá đầu vào hoặc
hồ sơ rà soát tài chính, người gửi xem trước email thực tế rồi xác nhận gửi duyệt
và thông báo. To điền sẵn email người chủ trì phòng đã phân công; theo xác nhận
bổ sung cùng ngày, người gửi có thể sửa To và thêm người liên quan vào CC. Phải
cập nhật xem trước sau khi sửa địa chỉ. Thay To/CC không thay người có quyền duyệt.
Email gửi cho To và CC cùng chứa link mở trực tiếp nhóm công việc; bản xem trước
hiển thị đúng link sẽ được xếp hàng gửi.
Màn hình thông báo đã xếp hàng, sau đó cập nhật kết quả gửi hoặc cần kiểm tra. Chặn gửi duyệt nếu
người chủ trì trùng người lập hồ sơ, không hoạt động, thiếu quyền duyệt hoặc email
không hợp lệ; phải chọn người chủ trì độc lập hợp lệ trước khi gửi. Mẫu email
quản lý tại `/admin/email-templates`, cùng khu vực mẫu báo giá và hợp đồng.

Hai hồ sơ dùng chung checklist và chung nguyên tắc "người lập không tự duyệt".
Hồ sơ đã gửi duyệt hoặc đã duyệt thì không sửa được nữa. Hồ sơ thương mại gắn
với phiên bản báo giá đã rà soát: báo giá thay đổi giá trị làm tăng phiên bản và
quay lại `Draft`, nên Tài chính phải rà soát lại trước khi gửi khách hàng.

## 3. Các đường vào dự án

NICON có nhiều đường vào hợp lệ; không bắt buộc mọi dự án đi qua cùng một chuỗi:

| Luồng | Trình tự chính | Điều kiện quan trọng |
|---|---|---|
| Design & Build | Lead → Cơ hội + Dự án → khảo sát/thiết kế/giá đầu vào → báo giá hoặc thống nhất trực tiếp giá trị → HĐ D&B → thiết kế/pháp lý/thi công | Hướng có Báo giá chỉ dùng Báo giá đã duyệt; hướng trực tiếp không cần Báo giá; thi công chỉ dùng IFC đã phát hành |
| Thiết kế trước hợp đồng | Cơ hội + Dự án → Concept/Basic/Detail → BOQ/báo giá → HĐ thiết kế hoặc D&B/thi công | Thiết kế được bắt đầu khi chưa có hợp đồng nhưng luôn thuộc Dự án |
| Đấu thầu | Dự án → gói thầu → kế hoạch/checklist/dự toán thầu → nộp → kết quả → thương thảo/HĐ | Kết quả, deadline và phiên bản dự toán phải có lịch sử |
| Tư vấn/báo giá sơ bộ | Dự án → khảo sát/Concept → suất đầu tư hoặc BOQ sơ bộ → báo giá → chuyển đổi nếu thành công | Không dùng BOQ sơ bộ làm hạn mức cấp phát thi công |

Lead sau chuyển đổi bị khóa theo hợp đồng hiện tại; Customer, Opportunity và
Operational Project được tạo/liên kết có kiểm soát. Việc hủy chuyển đổi chỉ được
thực hiện theo quyền và điều kiện an toàn dữ liệu hiện có.

Hợp đồng Upstream → Cơ hội nguồn → danh sách Lead đã chuyển đổi vào Cơ hội đó,
khớp khách hàng. Người xem chỉ thấy Lead thuộc phạm vi quyền riêng của mình và
mở được chi tiết từ link nguồn. Hợp đồng trực tiếp không có Cơ hội không tự gán
Lead; nhiều Lead đã chuyển đổi vào cùng Cơ hội được hiển thị đầy đủ trong phạm vi.

## 4. Các cổng kiểm soát chính

Đối chiếu thực hiện Hợp đồng: mở thông tin Hợp đồng → xem riêng công việc gắn
ContractId, hồ sơ QS đúng chiều/gắn ContractId và lịch thanh toán → mở hồ sơ nguồn
hoặc tab thanh toán. Giữ phạm vi quyền nguồn, phân biệt dữ liệu rỗng và bị hạn chế.
Không tính phần trăm hoàn thành chung và không suy diễn nguồn từ Dự án dùng chung.

### G0 — Khởi tạo Dự án

- Có khách hàng, mã/tên dự án và người phụ trách hợp lệ.
- Cơ hội, báo giá, hợp đồng và các module sau phải liên kết lại Dự án này.
- Khi tạo Dự án, hệ thống đề xuất mã tự động và người tạo được chỉnh mã trước
  khi lưu. Sau khi tạo, chỉ người có quyền chuyên biệt mới được đổi mã; yêu cầu
  phải có mã mới hợp lệ, lý do và đúng `rowVersion`. Hệ thống giữ mã cũ/mới,
  người đổi và thời điểm trong lịch sử, không đổi `OperationalProjectId`.
- Đổi tên thư mục Drive chạy bất đồng bộ trên đúng folder ID hiện có. Lỗi Drive
  không hoàn tác mã trong cơ sở dữ liệu nhưng phải hiển thị trạng thái và cho
  phép retry an toàn. Mã vẫn phải duy nhất toàn hệ thống; quy ước chi tiết vẫn
  theo quyết định Q-05 của NICON.

### G1 — Chuyển sang hợp đồng

- Hợp đồng Upstream có hai hướng đã được xác nhận ngày 16/10/2026: tạo từ Báo
  giá đã duyệt, hoặc tạo trực tiếp không cần Báo giá khi khách hàng đã biết và
  thống nhất giá trị Hợp đồng.
- Trước khi gửi báo giá cho khách, Tài chính – Kế toán phải duyệt hồ sơ rà soát
  điều kiện hợp đồng, bảo lãnh và điều kiện thanh toán. Báo giá chưa có hồ sơ
  duyệt thì không gửi được; báo giá cũng phải gắn Dự án vận hành để có chỗ ghi
  rà soát (xem 2.2).
- Khi chọn hướng có Báo giá, Hợp đồng Upstream phải tham chiếu Báo giá CRM đã
  được duyệt, còn hiệu lực, đúng khách hàng/Dự án và đúng Cơ hội khi Hợp đồng
  gắn Cơ hội. Hợp đồng Downstream không dùng Báo giá CRM đầu ra.
- Hợp đồng xác định rõ hướng `Upstream` hoặc `Downstream` và loại Design,
  Construction, Design & Build, Supply hoặc Subcontract.
- Một Dự án có thể có nhiều hợp đồng; không gộp chúng thành một bản ghi.
- Kinh doanh ghi lịch sử chăm sóc riêng cho Hợp đồng Upstream. Sau khi có ghi
  nhận đầu tiên, không được đổi chiều Hợp đồng (xác nhận người dùng 20/10/2026).
  API từ chối thay đổi và giữ nguyên lịch sử; không chặn đổi chiều chỉ vì chưa
  có ghi nhận chăm sóc, ngoài các ràng buộc hiện hành khác.
- Nhắc gia hạn/kết thúc: Kinh doanh chọn loại thông báo, ngày gửi riêng cho Hợp
  đồng (ngày Việt Nam) và người liên hệ của Khách hàng → xem trước email → xác
  nhận lịch tự động. Quản trị sửa mẫu tại `/admin/email-templates`. Worker gửi
  khi đến hạn, lưu kết quả và ghi lịch sử chăm sóc khi SMTP tiếp nhận. Thông tin
  Hợp đồng/người nhận hoặc quyền người lập lịch không còn hợp lệ thì yêu cầu
  kiểm tra lại; kết quả SMTP không xác định không được tự gửi lại. Email không
  thực hiện chuyển trạng thái Hợp đồng (xác nhận người dùng 20/10/2026).
  Từ lần bắt đầu gửi đầu tiên, khóa chiều Hợp đồng vĩnh viễn, kể cả khi kết
  quả gửi chưa xác định; lịch chỉ chờ gửi/chưa có lần thử không tạo khóa này
  (xác nhận người dùng 21/10/2026).

Luồng tái ký/phát triển Hợp đồng (chỉ đạo người dùng 22/10/2026): Kinh doanh
mở Hợp đồng Upstream đã ký → chọn lập Hợp đồng tiếp nối hoặc theo dõi Cơ hội →
chọn Renewal/Expansion, nhập lý do, xác nhận cùng Khách hàng và Dự án phù hợp →
lưu. Hướng trực tiếp tạo Hợp đồng Draft; hướng Cơ hội bắt đầu Prospecting và
tiếp tục đàm phán/Báo giá/ký theo CRM hiện có. Lần theo đuổi Lost lưu lý do và
không lập Hợp đồng; khi khách quay lại lập Cơ hội mới. Cả hai hướng giữ liên kết
nguồn, không sao chép nghĩa vụ/bằng chứng đã thực hiện. Tạo từ Cơ hội tiếp nối
kế thừa nguồn/mục đích/lý do; Hợp đồng mới vẫn phải đi qua gate ký/Pháp lý hiện
có. Email nhắc gia hạn không tự khởi tạo luồng này.

### G2 — Phát hành hồ sơ thi công

- Nhân viên hoàn thành đầu việc Thiết kế → hệ thống tự chuyển cho đúng một
  Design Lead/người duyệt đã chỉ định; CC chỉ nhận thông tin. Đầu việc bị trả
  lại quay về `InProgress` kèm lý do.
- Design Manager chọn hồ sơ Project Documents và người nhận Kinh doanh/CM/lãnh
  đạo → người được chỉ định chỉ xem, bình luận → Design Manager tổng hợp → ghi
  nhận đã trình khách → ghi nhận quyết định khách hàng.
- Công việc thiết kế thuê ngoài phải liên kết đồng thời Vendor và Hợp đồng
  Downstream cùng Dự án, đúng loại Design/Subcontract và đúng nhà cung cấp; NICON
  vẫn chỉ định một người phụ trách nội bộ.
- Mã nguồn hiện chặn xóa vật lý hồ sơ đã ghi nhận trình khách và dùng revision
  thay thế. Đây là mặc định an toàn trong khi NICON chưa chốt Q-08, không phải
  kết luận nghiệp vụ cuối cùng.
- Concept: Design Manager tạo từng phương án và revision R00; gắn mô hình 3D,
  phối cảnh, mặt bằng tổng thể/mái, mặt bằng từng tầng, mặt cắt, PDF trình bày.
  Kiểm checklist hoặc ghi lý do không áp dụng, chờ file đồng bộ, rồi trình nội
  bộ. Hệ thống khóa revision đã trình. Sau khi tổng hợp, Design Manager ghi
  nhận việc Kinh doanh trình khách theo ISO, ý kiến nguyên văn của khách, nguồn
  bằng chứng, phản hồi và kết quả xử lý riêng với bình luận nội bộ. Quyết định yêu
  cầu sửa/từ
  chối mở R tiếp theo; phải phản hồi các ý kiến cũ trước khi trình bản mới.
  Quyết định duyệt khách cần biên bản TK-M03 đã ký riêng với hồ sơ trình, người
  ký và ngày ký. Chỉ chốt một phương án khi revision mới nhất được duyệt cùng
  biên bản đó; các phương án còn lại bị loại. TK-M02 không thay TK-M03; không
  yêu cầu khách đăng nhập/ký qua cổng hoặc trình xem 3D chuyên dụng để chốt.
- Basic Design chỉ mở Shop Drawing khi đủ điều kiện nội bộ/pháp lý và revision
  Basic Design mới nhất là `ClientApproved`.
- Shop/Detailed Drawing được duyệt, đưa vào gói IFC và phát hành phiên bản cụ
  thể sau khi revision Shop Drawing mới nhất là `ClientApproved`. Chỉ revision
  IFC đã phát hành mới được dùng làm chỉ dẫn thi công.

### G3 — Pháp lý

- Khi hợp đồng Upstream Design/Construction/DesignAndBuild chuyển
  `Signed → InProgress`, mục tiêu là tự sinh danh sách thủ tục áp dụng để PM
  phân công ngay. Hiện trạng yêu cầu PM/Admin mở wizard, kiểm tra danh sách đề
  xuất và xác nhận; tạo `DesignProject` không tự sinh permit.
- Wizard tạo hoặc tái sử dụng một package `LegalPermitting` của Operational
  Project. Nhiều hợp đồng cùng dự án dùng chung package, không tạo permit/task
  trùng, nhưng mỗi hợp đồng giữ activation audit riêng.
- PM chọn chủ trì Pháp lý. Chủ trì phân công từng permit cho thành viên dự án
  đang hoạt động, kể cả khác phòng ban; mỗi permit có approver độc lập, lịch,
  WBS, predecessor và history riêng.
- Trạng thái cơ quan của `PermitChecklistItem` và trạng thái công việc nội bộ
  của `DepartmentWorkTask` là hai luồng độc lập. Chỉ permit `Issued` có ngày cấp
  và hồ sơ được cấp mới cho assignee trình task `Completed`; approver duyệt sang
  `Verified` hoặc trả về `InProgress` kèm lý do.
- Giấy phép có ngày hết hạn phải còn hiệu lực tại thời điểm sử dụng.
- Pháp lý/Admin soạn phiên bản mẫu nháp; CM/Admin có quyền xuất bản duyệt độc
  lập và ghi nguồn xác nhận. Người từng soạn/chỉnh sửa không tự xuất bản. Mẫu
  Published bất biến; bản mới bắt đầu từ Draft, Retired chỉ ngừng áp dụng mới.
- PM chọn mẫu Published khi tạo gói; server chặn thiếu mục bắt buộc. Mẫu có tên
  và mô tả phạm vi bốn ngôn ngữ, chưa tự khớp loại/quy mô. Dự án lưu snapshot,
  người/thời điểm áp dụng, không bị đổi khi mẫu được nâng phiên bản hoặc retire.
- Dự án cũ áp dụng chủ động qua preview, giữ lịch gói, hồ sơ và tiến độ sẵn có;
  chỉ thêm/liên kết permit/task thiếu. Bản đầu không đổi mẫu hay miễn mục bắt buộc.
- Q-09/D-04 về danh mục thực tế theo loại/quy mô vẫn là Open decision do
  CM/NICON chốt. Cờ bắt buộc checklist không tự thay đổi gate khởi công hiện có.

### G4 — Đủ điều kiện khởi công

Hợp đồng Upstream D&B/thi công đã ký chỉ làm cho Dự án **đủ điều kiện tiếp nhận
vào Phòng CM**; nó không tự động cho phép khởi công. Cổng khởi công phải kiểm tra
trên server, lưu người xác nhận và snapshot bằng chứng:

1. Hợp đồng Upstream D&B/thi công còn hiệu lực.
2. Phạm vi IFC cần cho mốc khởi công đã phát hành đúng revision.
3. Giấy phép bắt buộc cho mốc đó đã được cấp và còn hiệu lực.
4. WBS/Gantt, mốc, đường găng và Baseline S-Curve đã công bố.
5. BCH, người chịu trách nhiệm và kế hoạch nguồn lực đã được phê duyệt.
6. Budget Baseline đã khóa; gói Downstream thiết yếu đã sẵn sàng theo kế hoạch.
7. Các điều kiện theo dự án như bàn giao mặt bằng, biện pháp thi công và kế
   hoạch HSE được đánh dấu bắt buộc khi NICON/template dự án yêu cầu.

Nếu thiếu một điều kiện bắt buộc, trạng thái chỉ là `CM Preparation`, không phải
`Ready to Start` hoặc `In Construction`.

### G5 — Nghiệm thu, hoàn công và bàn giao

- Có nghiệm thu được duyệt, hồ sơ hoàn công đủ category bắt buộc, commissioning
  hoàn tất và không còn Punchlist chặn.
- Bàn giao cần checklist hoàn tất, ít nhất một bên ký và quyền hoàn tất riêng.
- Hồ sơ thanh toán và nghĩa vụ hợp đồng vẫn theo Module 6; bàn giao kỹ thuật
  không tự động đồng nghĩa đã thu/chi xong.

## 5. Module 4 — Thi công và nghiệm thu

### 5.1. Điểm bắt đầu và Giai đoạn 1 — Phòng CM chuẩn bị

```text
HĐ Upstream D&B/thi công đã ký
  ↓
Operational Project hiện có được chuyển vào hàng đợi CM Preparation
  ↓
PM Phòng CM lập và trình:
  ├─ WBS, Gantt, mốc, đường găng, Baseline S-Curve
  ├─ Sơ đồ BCH: Chỉ huy trưởng, Field/QA-QC, Site QS, HSE, Thủ kho
  ├─ Kế hoạch vật tư, thiết bị/máy, nhân công theo tiến độ
  ├─ Bid Tabulation và kế hoạch HĐ Downstream cùng Module 5/6
  └─ Budget Baseline: vật tư, nhân công, máy, chi phí BCH
  ↓
Cổng G4 kiểm tra điều kiện khởi công
```

Baseline đã công bố không bị ghi đè. Mỗi lần điều chỉnh tạo version, lý do,
người duyệt và lịch sử; thay đổi phạm vi/giá trị hợp đồng phải liên kết VO phù
hợp. Người dùng xác nhận ngày 24/10/2026: độ chậm S-Curve là `% kế hoạch −
% thực tế` theo điểm phần trăm; kỳ linh hoạt ngày/tháng/năm. Nguồn % thực tế,
cách tổng hợp theo kỳ và thẩm quyền publish/mở khóa vẫn cần NICON xác nhận.

### 5.2. Giai đoạn 2 — BCH thực hiện, Phòng CM kiểm soát chéo

| Luồng | BCH tại hiện trường | Phòng CM tại văn phòng | Đầu ra/liên kết |
|---|---|---|---|
| Nguồn lực | Gửi đề xuất vật tư, thiết bị/máy hoặc nhân công theo WBS và ngày cần | Duyệt tầng 2 khi ngoài kế hoạch/vượt hạn mức | Vật tư chuyển MR Module 5; máy/nhân công sau CM duyệt chuyển Cung ứng lập hợp đồng và cung cấp cho công trình, sau đó FA thanh toán theo hồ sơ hợp lệ |
| Nhật ký | Ghi thời tiết, tổ đội, nhân sự, thiết bị, vật tư, công việc, sự cố và ảnh | Kiểm tra thiếu dữ liệu và đối soát với tiến độ | Bằng chứng ngày; không tự coi Draft/offline là số liệu KPI |
| QA/QC | Lập biên bản lấy mẫu, thí nghiệm, chứng chỉ, checklist kiểm tra theo IFC | Thẩm định hồ sơ và kiểm tra đột xuất | Metadata/audit ở Nicon, file đồng bộ `04_Thi_cong_Nghiem_thu` |
| Punchlist | Ghi lỗi, vị trí, ảnh, nguyên nhân, người xử lý và hạn | Giám sát lỗi quá hạn, xác nhận đóng/mở lại | Lỗi chặn tham gia điều kiện nghiệm thu/bàn giao |
| HSE | Báo cáo vi phạm và bằng chứng, thực hiện khắc phục | Xác nhận độc lập, theo dõi hạn và đóng vi phạm | Sự kiện HSE xác nhận mới là bằng chứng KPI |
| Tiến độ | Cập nhật tiến độ và bằng chứng thực tế | So với Baseline S-Curve, kỳ linh hoạt ngày/tháng/năm | `% kế hoạch − % thực tế` **lớn hơn 5 điểm phần trăm** tạo cảnh báo đỏ và yêu cầu giải trình/kế hoạch phục hồi; đúng 5 điểm chưa vượt ngưỡng |
| QS Upstream | Đo và lập đợt nghiệm thu với CĐT/TVGS | Duyệt tầng 2 | Liên kết HĐ Upstream, WBS, BOQ/VO và mốc phải thu |
| QS Downstream | Đo khối lượng tổ đội/thầu phụ | Duyệt tầng 2 | Liên kết HĐ Downstream và hồ sơ phải trả |

Người tạo MR, hồ sơ QA/QC, QS, HSE hoặc VO không được đồng thời thực hiện bước
thẩm định độc lập của Phòng CM trên cùng bản ghi. Quy tắc này phải được kiểm tra
tại API, không chỉ ẩn nút trên giao diện.

### 5.3. Đề xuất nguồn lực

```text
BCH tạo đề xuất có loại + WBS + ngày cần + khối lượng + lý do
  ↓
Server kiểm tra Dự án, quyền, baseline và hạn mức
  ├─ Trong kế hoạch/hạn mức → luồng duyệt thông thường
  └─ Ngoài kế hoạch/vượt hạn mức → CM review độc lập
       ├─ Từ chối → không tạo commitment hoặc chứng từ downstream
       └─ Duyệt
            ├─ Vật tư → MR Module 5 theo Execution BOQ được duyệt
            └─ Máy/nhân công → Cung ứng lập hợp đồng, cung cấp cho công trình
                                  → FA xử lý thanh toán theo hồ sơ ISO hợp lệ
```

Hiện tại phần mềm đã có đề xuất Material/Equipment/Labour và CM level-2 review.
Material đã tạo được MR vật tư và chặn khi vượt allowance còn lại. Equipment và
Labour giữ handoff `ReadyForProcurementAndFinance` để tương thích API cũ.
**Partial:** inbox Cung ứng đã bổ sung lập HĐ Downstream nháp, liên kết một HĐ
cho mỗi đề xuất (một HĐ nhận nhiều đề xuất), đổi liên kết trước cung cấp và xác
nhận cung cấp bằng tài liệu cùng Dự án. Sau xác nhận không đổi liên kết. Lập
nháp không tự liên kết hoặc ký HĐ; người có quyền HĐ tiếp tục quy trình ký hiện
có. Chưa có cung cấp từng phần hoặc tạo Payment Request/gate ISO tự động.
Chuỗi CM → Cung ứng → FA đã được NICON xác nhận trong phản hồi do người dùng
cung cấp 18/10/2026; chưa xác định ngày trả lời gốc. Mỗi bước giữ stable ID,
actor và lịch sử; không tự coi CM duyệt là đã cung cấp hoặc đã thanh toán.
Các dải 85% vàng, 95% cam và trên 100% đỏ/chặn chưa thay thế validation hiện có
cho đến khi chốt mẫu số, quy đổi đơn vị, làm tròn, reversal và BOQ bằng 0.

### 5.4. Nghiệm thu và thanh toán

```text
Upstream QS được duyệt
  → cung cấp bằng chứng cho mốc phải thu Module 6

Thanh toán Downstream theo khối lượng đã nghiệm thu:
Downstream QS được duyệt
  + HĐ Downstream đã ký/phụ lục hợp lệ
  + Phiếu nhập kho Posted, trừ reversal (khi gói có vật tư)
  → đủ điều kiện để Kế toán review Payment Request
```

**Partial:** Payment Request phải trả nay có thể liên kết một Downstream QS batch
đã CM duyệt, đang hiệu lực, cùng HĐ và Dự án. API kiểm tra lại liên kết tại lúc
lập/sửa, xác nhận hồ sơ và phê duyệt; UI chỉ liệt kê QS phù hợp với HĐ đã chọn.
Liên kết là tùy chọn để giữ luồng tạm ứng/trả trước; chưa có ma trận ISO chứng từ
theo loại package, đối soát khoản tiền với QS hoặc tự động tạo Payment Request.
Khi QS bị thay thế sau khi trình, tạo revision của cùng Payment Request, lưu bản
đã trình, giữ số hóa đơn và duyệt lại từ Draft. Nếu QS bị thay thế sau duyệt,
không chi tiền đến khi revision mới được duyệt (quyết định NICON 20/10/2026).

`Đủ điều kiện review` không phải tự động thanh toán. Supply-only, labour-only,
equipment và mixed subcontract đối chiếu ma trận chứng từ trong bộ ISO NICON
đã gửi; thiếu mục nào yêu cầu FA, chị K/Kiara, bổ sung riêng mục đó. Không tạo
chứng từ giả để đủ three-way matching. Bộ nguồn `docs/Nicon/ISO/` đã được nhận
ngày 20/10/2026: quy trình `TQT` có thanh toán, quyết toán thầu phụ và khách
hàng; quy trình `MH` có đề nghị mua, báo giá, đặt hàng, đề nghị thanh toán;
quy trình `LK` có nhập/xuất kho và theo dõi thiết bị. Chưa có ma trận chứng từ
được FA/CM xác nhận cho bốn loại package, nên chưa thể khẳng định hồ sơ đủ để
mở gate thanh toán. Quy trình `MH` còn nêu đề nghị trả trước theo đơn đặt hàng,
và `TQT-M07` là mẫu tạm ứng; hai trường hợp này chưa có điều kiện riêng được
duyệt nên gate khối lượng/phiếu nhập kho ở trên không quyết định điều kiện cho
chúng. Xem
[quy trình TQT](<Nicon/ISO/D.Quy trình chi tiết/D.6. Quy trình thanh toán, quyết toán công trình/Quy trình thanh quyết toán.doc>)
và [quy trình MH](<Nicon/ISO/D.Quy trình chi tiết/D.7. Quy trình mua hàng, đánh giá nhà cung ứng, thầu phụ- co chinh sua/Quy trình mua hàng, đánh giá NCC.doc>).

**Target — ISO, BCH và tổng hợp CM đã xác nhận:** mỗi Dự án có BCH riêng,
nhưng một nhân sự có thể tham gia nhiều Dự án đồng thời; quyền vẫn theo từng
Dự án. Mẫu cho từng quy trình/bước lấy từ bộ ISO đã gửi, kiểm kê và báo riêng
form thiếu cho NICON. Bộ nguồn đã nhận có `TC-M01`, `TC-M04`–`TC-M09`,
`TC-M24` và 19 workbook nghiệm thu nội bộ `TC-M28`. Quy trình dẫn chiếu
`TC-M02`, `TC-M23`, `TQT-M00`, `TC-M00` nhưng chưa thấy file cùng mã; `TC-M15`
và `TC-M24` còn có nhiều nội dung dưới cùng mã. Cần NICON xác minh phiên bản
và điều kiện áp dụng từng mẫu.
Màn hình CM tổng hợp tiến độ, khối lượng hoàn thành
(nghiệm thu đầu ra/đầu vào tách biệt), giá trị thực hiện so với dự toán, QA/QC
và an toàn. Công thức/kỳ báo cáo và mapping ISO còn cần đối chiếu; xác nhận yêu
cầu không phải bằng chứng chức năng đã triển khai.

### 5.5. Hiện trạng Module 4 trong mã nguồn

| Năng lực | Trạng thái thực tế | Khóa dữ liệu hiện tại |
|---|---|---|
| Construction task/WBS, dependency, progress | Hiện có; chưa có baseline snapshot/S-Curve/resource loading/critical-path engine | `DesignProjectId` |
| Site diary | Hiện có Draft → Submitted → Confirmed; chưa có media/offline queue đầy đủ | `DesignProjectId` |
| Punchlist | Hiện có Open → InProgress → Fixed → Verified, root cause/KPI attribution | `DesignProjectId` |
| HSE violation | Hiện có lifecycle, correction, concurrency và evidence paths | `OperationalProjectId` |
| Partial acceptance | Hiện có workflow chung; chưa phân hướng Upstream/Downstream QS | `DesignProjectId` |
| As-built | Hiện có category, workflow và completeness | `DesignProjectId` |
| Handover | Hiện có readiness, commissioning, checklist, signatory và row version | `DesignProjectId` |
| CM preparation/BCH organization/Budget Baseline | Có aggregate versioned và readiness snapshot; baseline vẫn là document reference, chưa có dữ liệu cấu trúc/S-Curve engine | `OperationalProjectId` |
| Resource request | Partial: Material tạo MR; Equipment/Labour có inbox, lập HĐ nháp/liên kết HĐ và xác nhận cung cấp kèm lịch sử; chưa có Finance/ISO gate | `OperationalProjectId` + WBS/BOQ |
| QA/QC dossier | Có template snapshot, checklist, IFC và loại minh chứng theo từng tài liệu; checklist ISO bắt buộc theo loại công trình chưa chốt | `OperationalProjectId` + WBS |
| Site QS hai hướng | Có Upstream/Downstream, contract/BOQ/milestone, evidence và correction lineage; chưa handoff Finance | `OperationalProjectId` + WBS/contract/BOQ |

Không mở rộng các aggregate mới dựa trên `DesignProjectId`. Phần hiện có cần
resolve an toàn qua Design Project sang Operational Project và được migration
dần; không được phá API/dữ liệu cũ trong một lần chuyển đổi không có rehearsal.

## 6. Phân loại BOQ và nguồn chuẩn

| Loại | Mục đích | Chủ trì | Có dùng làm hạn mức MR/kho? |
|---|---|---|---|
| BOQ sơ bộ | Báo giá sớm/suất đầu tư | Sales/Tendering | Không |
| Tender Estimate/BOQ dự thầu | Lập và so sánh giá dự thầu | Tendering/QS văn phòng | Không trực tiếp |
| Design quantity takeoff | Khối lượng kỹ thuật từ hồ sơ thiết kế | Design/QS | Chỉ là nguồn đối chiếu |
| Execution BOQ revision | Hạn mức thực hiện đã duyệt sau hợp đồng | PM CM + Procurement/QS theo quyền | Có; revision `Approved` hiện hành |
| Final BOQ | Phiên bản thực hiện cuối khi Dự án hoàn tất, gồm VO đã duyệt | PM CM/Finance | Dùng quyết toán, báo cáo và KPI |

Giá đầu vào, material-rate catalog, Quote BOQ và Execution BOQ là các aggregate
khác nhau. Việc import phải có preview, mapping, lỗi theo sheet/ô, checksum hoặc
provenance, người xác nhận và không hard-code tên sheet từ file mẫu.
Dòng tiêu đề nhóm, tiêu đề cột lặp và tổng phụ không được tính thành dòng đơn
giá; dòng chi tiết phải có tên, đơn vị, khối lượng dương và đơn giá không âm.

Workbook BOQ khách hàng được nhập tại Draft revision của material-rate catalog.
Quote BOQ chỉ áp dụng revision Approved phù hợp và lưu snapshot để lập báo giá;
không có luồng import workbook trực tiếp vào Quote. Bảng nhập nhiều dòng hoặc
thêm từng dòng tại Quote chỉ bổ sung ngoại lệ cục bộ, không thay đổi catalog và
không thay thế quy trình chuẩn hoá workbook. Không dùng vùng nhập text/clipboard
tự do cho BOQ của Quote.

**Implemented — luồng chọn danh mục → nhập đúng trường (17/10/2026):**

1. Vào danh mục đơn giá BOQ; chọn một cấu hình mẫu đã có hoặc tạo danh mục mới.
   Nếu cần bộ trường khác, sao chép cấu trúc rồi sửa sheet/field và nhãn bốn
   ngôn ngữ **trước khi tạo phiên bản đầu tiên**. Danh mục cũ không có cấu hình
   giữ mẫu nhập 5 cột hiện tại; không tự chuyển dữ liệu.
2. Tạo Draft revision; chọn sheet để thêm/sửa/bỏ dòng với đúng trường của sheet.
   Đổi sheet reset form mới; đổi danh mục/revision reset ngữ cảnh nhập. Không
   cho mang field của danh mục cũ sang danh mục mới.
3. Hoặc tải mẫu riêng của danh mục (VI/EN/ZH/JA), giữ nguyên sheet/header/dấu
   nhận diện, điền từ hàng 3 bằng số/văn bản, không dùng công thức. Kiểm tra file
   trước; hệ thống hiển thị các dòng hợp lệ và cảnh báo **thay toàn bộ Draft**.
   Xác nhận mới lưu atomic; sai field/category/hash hoặc revision stale bị từ
   chối và không ghi đè. File trống không được import để xóa dữ liệu.
4. Dòng có giá tạo canonical code theo sheet + mã nguồn, quantity, unit price
   và amount để luồng duyệt/Quote hiện tại sử dụng. Cùng STT ở sheet khác không
   trùng nhau; trùng code trong cùng sheet bị chặn. Dòng tham khảo chỉ lưu cùng
   revision, không cộng tổng hay dùng làm allowance. Giá thành phần/kích thước
   được lưu đúng trường để đối chiếu, không tự suy tổng giá/khối lượng.
5. Từ revision đầu, cấu hình không đổi; muốn đổi cấu trúc tạo danh mục mới.
   Danh mục đã cấu hình chỉ được ngừng hoạt động, không hard-delete qua luồng
   cũ. Chỉ applicable approved Execution BOQ làm hạn mức MR/kho như trước.

Đây là mẫu nhập chuẩn hóa dựa trên cấu trúc nguồn, không phải tính năng import
nguyên trạng mọi workbook khách hàng. Cover/summary không được tạo thành dòng
chi phí trùng; giữ file nguồn dưới docs bất biến. Các khả năng baseline/hierarchy,
provenance từng cell, công thức Excel và chuyển schema tự động vẫn là Target.

## 7. Google Drive và tài liệu

### 7.1. Cây mục tiêu NICON

```text
[ProjectCode]_[ProjectName]
├─ 01_CRM_PreDesign
│  └─ 01_Khao_sat
├─ 02_Thiet_ke
│  ├─ 01_So_bo_Concept
│  ├─ 02_Co_so
│  └─ 03_Chi_tiet_ShopDrawing
├─ 03_Xin_phep_Phap_ly
├─ 04_Thi_cong_Nghiem_thu
├─ 05_Cung_ung_Vat_tu
└─ 06_Tai_chinh_Hop_dong
```

### 7.2. Tương thích hiện tại

Mã nguồn hiện lưu `01_Khao_sat` và `01_CRM_PreDesign` thành hai category sibling
cấu hình độc lập. Đây là hiện trạng phải giữ khi nâng cấp để không làm mất liên
kết. Việc đưa khảo sát vào dưới `01_CRM_PreDesign` cần migration topology riêng,
kiểm kê folder/file, xử lý xung đột và khả năng rollback; không đổi tên âm thầm.

Drive chỉ là nơi lưu binary. Nicon giữ metadata, nguồn, quyền, version, checksum,
trạng thái đồng bộ, conflict và audit. Trạng thái `Synced` chỉ được đặt sau khi
external write thành công; lỗi phải retry được và không tạo thành công giả.

## 8. Dashboard và KPI

Thống kê trúng thầu tại danh sách Gói thầu: chọn kỳ nộp lần đầu (giờ Việt Nam),
khách hàng/người phụ trách → Áp dụng → xem số gói trúng trên toàn bộ gói đã nộp
→ mở danh sách đối chiếu. Nộp bổ sung không tăng số gói; chờ kết quả và hủy sau
nộp vẫn ở mẫu số. Dùng kết quả hiện tại nên kỳ cũ có thể thay đổi; KPI đã khóa
giữ nguyên snapshot. Hồ sơ thiếu bằng chứng nộp được cảnh báo riêng, không gán
ngày nộp giả. Quyền xem thống kê theo quyền xem Gói thầu hiện hữu.

Khung mục tiêu có 11 vị trí NICON. Mã nguồn hiện có 19 định nghĩa cho sáu nhóm
scorecard: Sales, Tendering, Design, Site, Procurement và Project Accounting.
Các vị trí mục tiêu PM CM, Chỉ huy trưởng, Field/QA-QC, Site QS, Legal và HR chưa
đều có scorecard độc lập.

KPI nghiệp vụ theo Dự án chỉ tính từ sự kiện terminal có project, người chịu
trách nhiệm, timestamp, status và version. Riêng `VerifiedWorkTaskCount` được
phép lấy đầu việc nội bộ không có project khi đầu việc có người chịu trách
nhiệm, trạng thái `Verified`, người xác nhận độc lập, timestamp và version.
Không có mẫu số/bằng chứng thì trả `MissingData`, không tự đổi thành 0. Kỳ đã
khóa là snapshot bất biến.

## 9. Quyết định còn cần NICON chốt

1. Q-01: cách giữ/chuyển nhóm việc khi có một hoặc nhiều hợp đồng.
2. Q-02: duyệt và KPI cho công việc nội bộ.
3. Q-03: PM Dự án hay người phụ trách Cơ hội được giao việc liên phòng.
4. Q-04: vị trí tổ chức của Cung ứng và Pháp lý trong năm phòng baseline.
5. Q-05: quy ước mã dự án chi tiết.
6. Q-06 đến Q-08 và Q-10: quyền bình luận/đồng ý, CC mặc định, xóa hồ sơ và ranh
   giới duyệt của Design Lead/Design Manager.
7. Q-09: template giấy phép theo loại/quy mô Dự án.
8. Q-11: hợp đồng import MS Project đầy đủ gồm calendar, baseline và dữ liệu
   phân cấp; Team và Tiến độ hiện được tách thành hai tab liên kết.
9. Nguồn actual/cách tổng hợp S-Curve theo kỳ linh hoạt ngày/tháng/năm;
   quyền publish/unlock baseline; điều kiện VO. Công thức điểm phần trăm đã
   được người dùng xác nhận ngày 24/10/2026.
10. Hoàn tất mapping bộ ISO trong `docs/Nicon/ISO/` vào QA/QC/cổng khởi công
    theo loại công trình; NICON xác minh `TC-M02`, `TC-M23`, `TQT-M00`, `TC-M00`
    và các mã dùng cho nhiều nội dung, rồi bổ sung riêng form còn thiếu. Không
    cần chọn lại bộ mẫu chuẩn.
11. Đối chiếu chứng từ ISO cho supply, labour, equipment và mixed package;
    FA (chị K/Kiara) bổ sung mục thiếu, xác nhận ngoại lệ và mapping chưa rõ.
    Giữ riêng luồng Upstream/Downstream; áp dụng three-way matching khi gói có
    vật tư đã nghiệm thu và đối chiếu chứng từ phù hợp cho các gói còn lại.
    Chốt riêng điều kiện cho tạm ứng/trả trước trước khi mở gate tương ứng.
12. Quy tắc dải BOQ 85/95/>100: mẫu số, commitment, issue, reversal, conversion,
   precision và zero allowance.
13. Kế hoạch migration các record Module 2/3/4 từ `DesignProjectId` sang
   `OperationalProjectId`, gồm compatibility API và rollback.
14. Cây Drive đích và migration `01_Khao_sat` vào `01_CRM_PreDesign`.

Đã đối chiếu từng câu Q-01–Q-11 và các đầu vào bổ sung với ISO tại
`Nicon-QLVH.md` Mục 12. Riêng `NS2-M01` có 11 tiêu chí chấm điểm cá nhân và
quy trình `NS2` nêu trưởng bộ phận đánh giá, BGĐ dùng báo cáo cho quyết định
nhân sự; đây là nguồn tham chiếu cho Q-02, chưa định nghĩa quyền duyệt việc nội
bộ hoặc công thức KPI số hóa. Quy trình TK liệt kê bước trình khách/cơ quan,
không bổ sung yêu cầu đồng ý ở vòng trình nội bộ Q-06. Các tỷ lệ 5% trong mẫu
thanh toán không phải công thức cảnh báo trễ S-Curve.

Các điểm trên là blocker cho phần kiến trúc/dữ liệu tương ứng. Chỉ Product Owner
hoặc đại diện nghiệp vụ NICON được chấp nhận rủi ro yêu cầu.
