import type { ProcessItem } from "@/pages/admin/ProcessList";

export const generalProcesses: ProcessItem[] = [
  { id: "g1", title: "Quy trình kiểm soát tài liệu" },
  { id: "g2", title: "Quy trình đánh giá nội bộ" },
  { id: "g3", title: "Quy trình cải tiến" },
  { id: "g4", title: "Quy trình đánh giá rủi ro - cơ hội" },
  { id: "g5", title: "Quy trình xác định bối cảnh" },
];

export const ptcskhProcesses: ProcessItem[] = [
  { id: "p1", title: "Quy trình phát triển và chăm sóc khách hàng" },
  { id: "p2", title: "Quy trình giải quyết khiếu nại của khách hàng" },
  { id: "p3", title: "Quy trình đo lường sự thỏa mãn của khách hàng" },
];

export const dtProcesses: ProcessItem[] = [
  { id: "d1", title: "Quy trình đấu thầu" },
  { id: "d2", code: "DT-M02", title: "Phân chia công việc đấu thầu" },
  { id: "d3", code: "DT-M03", title: "Yêu cầu báo giá" },
  { id: "d4", code: "DT-M04", title: "Bảng phân chia công việc đấu thầu" },
  { id: "d5", code: "DT-M05", title: "Yêu cầu báo giá nhà cung cấp" },
  { id: "d6", code: "DT-M07", title: "Hợp đồng" },
  { id: "d7", code: "QLTC-QT01", title: "Quy trình tổng thể đấu thầu" },
];

export const tkProcesses: ProcessItem[] = [
  { id: "t1", code: "TK-M01", title: "Phiếu thu thập thông tin" },
  { id: "t2", code: "TK-PL1", title: "Hồ sơ thiết kế sơ bộ" },
  { id: "t3", code: "TK-PL2", title: "Thuyết minh thiết kế sơ bộ" },
  { id: "t4", code: "TK-BM02", title: "Biên bản nghiệm thu hồ sơ" },
  { id: "t5", code: "BM-BLĐ-QT01-08", title: "Biên bản bàn giao hồ sơ" },
  { id: "t6", code: "KT-M01", title: "Kiểm tra thiết kế kỹ thuật" },
  { id: "t7", code: "KT-BM02", title: "Biên bản nghiệm thu hồ sơ TKKT" },
  { id: "t8", code: "TK-M03", title: "Bàn giao hồ sơ thi công" },
];

export const tcProcesses: ProcessItem[] = [
  { id: "c0", code: "QLTC-QT02-01", title: "Thi công - Nghiệm thu - Bàn giao" },
  { id: "c1", title: "QT - Chuẩn bị" },
  { id: "c2", title: "QT - Duyệt mẫu vật tư và đề xuất vật tư" },
  { id: "c3", title: "QT - Duyệt và kiểm soát bản vẽ shopdrawings" },
  { id: "c4", title: "QT - Duyệt và kiểm soát tiến độ" },
  { id: "c5", title: "Danh mục hồ sơ nghiệm thu công việc" },
  { id: "c6", title: "QT - NT.Công việc" },
  { id: "c7", title: "QT - NT.Giai đoạn" },
  { id: "c8", title: "QT - NT.Bàn giao" },
  { id: "c9", title: "QT - Quản lý - điều động - bảo trì thiết bị" },
  { id: "c10", title: "Phụ lục các nguyên tắc an toàn thi công" },
  { id: "c11", title: "QT - ATLĐ_VSMT" },
  { id: "c12", code: "TC-M28", title: "Biên bản nghiệm thu nội bộ" },
  { id: "c13", title: "QT - Thầu phụ" },
  { id: "c14", title: "QT - Quản lý kho công trường" },
  { id: "c15", title: "QT - Phát sinh" },
  { id: "c16", title: "QT - Xử lý tình huống khẩn cấp" },
  { id: "c17", title: "QT - Xử lý kỷ luật" },
];

export const ttqtctProcesses: ProcessItem[] = [
  { id: "tq1", code: "01.TQT-QT", title: "Quy trình thanh toán, quyết toán" },
  { id: "tq2", code: "MH-M04", title: "Đề nghị thanh toán" },
  { id: "tq3", code: "TC-M14", title: "Yêu cầu thanh toán bằng tháng" },
  { id: "tq4", code: "TQT-M01", title: "Bảng tổng hợp" },
  { id: "tq5", code: "TQT-M02", title: "Đề nghị thanh toán" },
  { id: "tq6", code: "TQT-M03", title: "Phiếu chi" },
  { id: "tq7", code: "TQT-M04", title: "Quyết toán khách hàng" },
  { id: "tq8", code: "TQT-M06", title: "BB Thanh lý hợp đồng" },
  { id: "tq9", code: "TQT-M07", title: "Đề nghị tạm ứng" },
  { id: "tq10", code: "TQT-M08", title: "Thông báo thanh toán" },
  { id: "tq11", code: "TQT-M09", title: "Công văn thanh toán" },
];

export const qlnsProcesses: ProcessItem[] = [
  { id: "n1", title: "Quy trình hoạch định và tuyển dụng nhân sự" },
  { id: "n2", title: "Quy trình tuyển dụng - đào tạo" },
  { id: "n3", title: "Quy trình thử việc" },
  { id: "n4", title: "Quy trình xin nghỉ việc - nghỉ phép" },
  { id: "n5", code: "QLNS-M01", title: "Phiếu yêu cầu tuyển dụng" },
  { id: "n6", code: "QLNS-M02", title: "Phiếu đăng ký dự tuyển" },
  { id: "n7", code: "QLNS-M03", title: "Hợp đồng thử việc" },
  { id: "n8", code: "QLNS-M04", title: "Bảng đánh giá thử việc" },
  { id: "n9", code: "QLNS-M05", title: "Hợp đồng lao động" },
  { id: "n10", code: "QLNS-M06", title: "Quyết định bổ nhiệm" },
  { id: "n11", code: "QLNS-M07", title: "Quyết định khen thưởng / kỷ luật" },
  { id: "n12", code: "QLNS-M08", title: "Đơn xin nghỉ phép" },
  { id: "n13", code: "QLNS-M09", title: "Đơn xin thôi việc" },
];

export const mhdgncuProcesses: ProcessItem[] = [
  { id: "m1", title: "Quy trình mua hàng, đánh giá nhà cung ứng, thầu phụ" },
  { id: "m2", code: "MH-M02", title: "Yêu cầu báo giá" },
  { id: "m3", code: "MH-M03", title: "Đơn đặt hàng" },
  { id: "m4", code: "MH-M04", title: "Đề nghị thanh toán" },
  { id: "m5", code: "MH-M05", title: "Phiếu yêu cầu đánh giá NCC" },
  { id: "m6", code: "MH-M06", title: "Danh sách NCC ban đầu" },
  { id: "m7", code: "MH-M07", title: "Phiếu đánh giá NCC" },
  { id: "m8", code: "MH-M08", title: "Danh sách NCC được duyệt" },
  { id: "m9", code: "TC-DM-M04", title: "Phiếu yêu cầu vật tư" },
];
