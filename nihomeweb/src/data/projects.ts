import bma from "@/assets/project-bma.jpg";
import nbdc from "@/assets/project-nbdc.jpg";
import lhh from "@/assets/project-lhh.jpg";
import sports from "@/assets/project-sports.jpg";
import office from "@/assets/project-office.jpg";

export type ProjectStatus = "ongoing" | "completed";

export interface Project {
  id: string;
  img: string;
  gallery?: string[];
  name: string;
  client: string;
  location: string;
  scale: string;
  scope: string;
  status: ProjectStatus;
  year?: string;
  category?: string;
  description?: string;
  challenges?: string[];
  solutions?: string[];
  highlights?: { label: string; value: string }[];
}

export const projects: Project[] = [
  {
    id: "nha-may-bma",
    img: bma,
    gallery: [bma, office, nbdc],
    name: "Nhà Máy BMA",
    client: "Bảo Minh Ân Việt Nam",
    location: "KCN Hựu Thạnh, Tây Ninh",
    scale: "15.000 m²",
    scope: "Thiết kế và Thi công",
    status: "ongoing",
    year: "2024",
    category: "Nhà máy công nghiệp",
    description:
      "Dự án Nhà Máy BMA là tổ hợp sản xuất hiện đại với quy mô 15.000 m², được thiết kế theo tiêu chuẩn công nghiệp quốc tế. NICON đảm nhận trọn gói từ khâu thiết kế kiến trúc, kết cấu, M&E cho đến thi công xây dựng và bàn giao chìa khoá trao tay.",
    challenges: [
      "Yêu cầu tiến độ chặt chẽ trong vòng 10 tháng từ khởi công đến vận hành.",
      "Giải pháp kết cấu nhà xưởng nhịp lớn không cột giữa cho dây chuyền sản xuất.",
      "Tối ưu hệ thống thông gió và chiếu sáng tự nhiên để tiết kiệm năng lượng.",
    ],
    solutions: [
      "Áp dụng kết cấu thép tiền chế nhịp 30m với mái lấy sáng polycarbonate.",
      "Thi công song song nhiều hạng mục, quản lý tiến độ bằng phần mềm BIM 4D.",
      "Hệ thống M&E đồng bộ, dự phòng công suất cho mở rộng tương lai 30%.",
    ],
    highlights: [
      { label: "Diện tích", value: "15.000 m²" },
      { label: "Thời gian", value: "10 tháng" },
      { label: "Nhịp kết cấu", value: "30 m" },
      { label: "Tiêu chuẩn", value: "ISO 9001" },
    ],
  },
  {
    id: "nha-xuong-nbdc",
    img: nbdc,
    gallery: [nbdc, bma, lhh],
    name: "Nhà Xưởng NBDC",
    client: "Công ty TNHH NBDC VN",
    location: "KCN Giang Điền, Đồng Nai",
    scale: "8.500 m²",
    scope: "Thiết kế",
    status: "ongoing",
    year: "2024",
    category: "Nhà xưởng sản xuất",
    description:
      "NICON cung cấp dịch vụ thiết kế kiến trúc và kết cấu trọn gói cho nhà xưởng sản xuất NBDC tại KCN Giang Điền. Thiết kế tối ưu dây chuyền vận hành và đảm bảo tiêu chuẩn an toàn lao động.",
    challenges: [
      "Bố cục dây chuyền sản xuất phức tạp với nhiều khu vực chức năng.",
      "Yêu cầu tích hợp khu văn phòng điều hành và sản xuất trong cùng một khối.",
    ],
    solutions: [
      "Phân khu rõ ràng với luồng di chuyển một chiều, giảm chéo nhau.",
      "Thiết kế khu văn phòng 2 tầng tích hợp với view nhìn xuống xưởng.",
    ],
    highlights: [
      { label: "Diện tích", value: "8.500 m²" },
      { label: "Khu chức năng", value: "5" },
      { label: "Nhân sự dự kiến", value: "200" },
      { label: "Năm", value: "2024" },
    ],
  },
  {
    id: "nha-may-lhh",
    img: lhh,
    gallery: [lhh, bma, sports],
    name: "Nhà Máy Lâm Hiệp Hưng – Tân Toàn Phát",
    client: "Lam Hiệp Hưng & Tân Toàn Phát",
    location: "Tỉnh Bình Dương",
    scale: "250.000 m²",
    scope: "Thiết kế",
    status: "ongoing",
    year: "2023",
    category: "Tổ hợp công nghiệp",
    description:
      "Một trong những dự án quy mô lớn nhất NICON đã thực hiện: tổ hợp nhà máy 250.000 m² bao gồm khu sản xuất, kho bãi, văn phòng và khu phụ trợ. Thiết kế tổng thể được tối ưu cho hoạt động logistics nội bộ.",
    challenges: [
      "Quy hoạch tổng mặt bằng quy mô siêu lớn với nhiều khối công trình.",
      "Đồng bộ hạ tầng kỹ thuật trên diện tích lớn.",
    ],
    solutions: [
      "Quy hoạch theo mô-đun, dễ dàng mở rộng và thay đổi công năng.",
      "Hệ thống đường nội bộ thiết kế cho xe container 40 feet.",
    ],
    highlights: [
      { label: "Tổng diện tích", value: "250.000 m²" },
      { label: "Khối công trình", value: "12" },
      { label: "Đường nội bộ", value: "5,2 km" },
      { label: "Năm", value: "2023" },
    ],
  },
  {
    id: "ttdtt-thu-duc",
    img: sports,
    gallery: [sports, office, bma],
    name: "Trung Tâm Thể Dục Thể Thao Thủ Đức",
    client: "Thủ Thiêm Group",
    location: "Thủ Đức, TP.HCM",
    scale: "12.000 m²",
    scope: "Thiết kế",
    status: "ongoing",
    year: "2024",
    category: "Công trình công cộng",
    description:
      "Trung tâm thể dục thể thao đa năng phục vụ cộng đồng tại Thủ Đức, gồm nhà thi đấu trong nhà, hồ bơi, phòng gym và khu sân ngoài trời.",
    challenges: [
      "Thiết kế nhịp lớn cho nhà thi đấu không cột giữa.",
      "Đảm bảo tiêu chuẩn âm học và ánh sáng cho thi đấu chuyên nghiệp.",
    ],
    solutions: [
      "Kết cấu vòm thép nhịp 45m, tối ưu trọng lượng.",
      "Hệ thống mái lấy sáng kết hợp đèn LED điều khiển thông minh.",
    ],
    highlights: [
      { label: "Diện tích", value: "12.000 m²" },
      { label: "Nhịp mái", value: "45 m" },
      { label: "Sức chứa", value: "2.000 chỗ" },
      { label: "Năm", value: "2024" },
    ],
  },
  {
    id: "noi-that-b37",
    img: office,
    gallery: [office, sports, nbdc],
    name: "Nội Thất – Văn Phòng B37",
    client: "Nihome",
    location: "Thủ Đức, TP.HCM",
    scale: "1.200 m²",
    scope: "Thiết kế",
    status: "ongoing",
    year: "2024",
    category: "Nội thất văn phòng",
    description:
      "Thiết kế nội thất văn phòng hiện đại với phong cách tối giản, tối ưu không gian làm việc cho 80 nhân sự. Tích hợp khu pantry, phòng họp và khu thư giãn.",
    challenges: [
      "Tận dụng tối đa diện tích cho 80 nhân viên.",
      "Tạo không gian linh hoạt giữa làm việc cá nhân và teamwork.",
    ],
    solutions: [
      "Bố cục mở kết hợp các phòng họp kính linh hoạt.",
      "Sử dụng vật liệu tự nhiên, ánh sáng vàng tạo cảm giác ấm cúng.",
    ],
    highlights: [
      { label: "Diện tích", value: "1.200 m²" },
      { label: "Sức chứa", value: "80 người" },
      { label: "Phòng họp", value: "6" },
      { label: "Năm", value: "2024" },
    ],
  },
  {
    id: "nha-may-trimas",
    img: bma,
    gallery: [bma, lhh, office],
    name: "Nhà Máy Trimas Việt Nam",
    client: "Rieke Packaging Vietnam Co., Ltd",
    location: "VSIP IIA, TP.HCM",
    scale: "10.000 m²",
    scope: "Thiết kế và Thi công",
    status: "completed",
    year: "2022",
    category: "Nhà máy công nghiệp",
    description:
      "Dự án trọn gói thiết kế và thi công nhà máy sản xuất bao bì cho Trimas tại VSIP IIA. Hoàn thành đúng tiến độ và bàn giao đạt chuẩn quốc tế.",
    challenges: [
      "Yêu cầu phòng sạch tiêu chuẩn quốc tế cho khu vực sản xuất.",
      "Tiến độ thi công chặt chẽ với cam kết phạt hợp đồng.",
    ],
    solutions: [
      "Áp dụng quy trình QA/QC nghiêm ngặt, đặc biệt cho khu phòng sạch.",
      "Quản lý tiến độ bằng MS Project, họp giao ban hàng tuần.",
    ],
    highlights: [
      { label: "Diện tích", value: "10.000 m²" },
      { label: "Tiêu chuẩn", value: "ISO Class 8" },
      { label: "Thời gian", value: "9 tháng" },
      { label: "Năm", value: "2022" },
    ],
  },
  {
    id: "nha-kho-apm",
    img: lhh,
    gallery: [lhh, nbdc, bma],
    name: "Nhà Kho APM",
    client: "Auto Components Việt Nam",
    location: "KCN Việt Nam – Singapore",
    scale: "6.500 m²",
    scope: "Thiết kế",
    status: "completed",
    year: "2022",
    category: "Nhà kho logistics",
    description:
      "Thiết kế nhà kho logistics cho Auto Components Việt Nam với hệ thống kệ cao tầng và khu vực xếp dỡ tối ưu cho xe container.",
    challenges: ["Tối ưu chiều cao thông thuỷ cho hệ kệ 8m.", "Khu vực dock loading cho 6 container đồng thời."],
    solutions: ["Mái nhà chiều cao 12m, hệ kèo nhẹ.", "Bố trí 6 dock leveler với mái che chống mưa."],
    highlights: [
      { label: "Diện tích", value: "6.500 m²" },
      { label: "Chiều cao", value: "12 m" },
      { label: "Dock loading", value: "6" },
      { label: "Năm", value: "2022" },
    ],
  },
  {
    id: "nha-may-jojo",
    img: nbdc,
    gallery: [nbdc, bma, lhh],
    name: "Nhà Máy JOJO",
    client: "Phạm – Asset",
    location: "KCN Hựu Thạnh, Long An",
    scale: "7.800 m²",
    scope: "Thiết kế",
    status: "ongoing",
    year: "2024",
    category: "Nhà máy công nghiệp",
    description:
      "Thiết kế nhà máy sản xuất JOJO với yêu cầu cao về vệ sinh an toàn thực phẩm và môi trường làm việc.",
    challenges: ["Tiêu chuẩn HACCP cho khu chế biến.", "Phân luồng nguyên liệu và thành phẩm rõ ràng."],
    solutions: ["Thiết kế sàn epoxy chống thấm, dễ vệ sinh.", "Cửa air-shower phân chia khu sạch và bẩn."],
    highlights: [
      { label: "Diện tích", value: "7.800 m²" },
      { label: "Tiêu chuẩn", value: "HACCP" },
      { label: "Khu sạch", value: "3" },
      { label: "Năm", value: "2024" },
    ],
  },
  {
    id: "khach-san-d22",
    img: sports,
    gallery: [sports, office, lhh],
    name: "Khách sạn D22",
    client: "Nihome",
    location: "Thủ Đức, TP.HCM",
    scale: "4.500 m²",
    scope: "Thiết kế và Thi công",
    status: "ongoing",
    year: "2024",
    category: "Khách sạn",
    description:
      "Khách sạn 4 sao với 80 phòng nghỉ, nhà hàng tầng trệt và khu spa, được thiết kế theo phong cách hiện đại tối giản kết hợp yếu tố Á Đông.",
    challenges: [
      "Thiết kế đa công năng trên diện tích đất hạn chế.",
      "Yêu cầu cách âm cao giữa các phòng nghỉ.",
    ],
    solutions: [
      "Bố trí thông minh với khu công cộng tầng thấp, phòng nghỉ tầng cao.",
      "Hệ tường thạch cao 2 lớp với bông cách âm dày 100mm.",
    ],
    highlights: [
      { label: "Diện tích", value: "4.500 m²" },
      { label: "Số phòng", value: "80" },
      { label: "Tầng cao", value: "12" },
      { label: "Năm", value: "2024" },
    ],
  },
];

export const getProjectById = (id: string) => projects.find((p) => p.id === id);
