// Service offerings — adapted from nicon.vn
export type ServiceItem = {
  slug: string;
  title: string;
  shortTitle: string;
  tagline: string;
  intro: string;
  sections: { heading: string; body: string[] }[];
  highlights: string[];
};

export const services: ServiceItem[] = [
  {
    slug: "design-and-build",
    shortTitle: "Design & Build",
    title: "Tổng thầu Thiết kế và Thi công (D&B)",
    tagline: "Một đầu mối — toàn bộ vòng đời dự án.",
    intro:
      "Phương thức Design & Build (D&B) và EPC là hai phương pháp phổ biến nhất trong xây dựng công nghiệp và dân dụng. NICON đã hệ thống hoá quy trình D&B từ ngày đầu thành lập và liên tục hoàn thiện qua hơn 150 dự án.",
    sections: [
      {
        heading: "Lợi thế của phương thức D&B / EPC",
        body: [
          "Tối thiểu hóa nghĩa vụ quản lý cho chủ đầu tư — NICON đảm nhận toàn bộ điều phối và quản lý dự án.",
          "Giảm thiểu rủi ro không nhất quán giữa thiết kế và thi công.",
          "Linh hoạt đẩy nhanh tiến độ ngay cả khi thiết kế chưa hoàn chỉnh, giảm chi phí phát sinh.",
          "Chi phí quản lý hợp lý — chủ đầu tư dễ ước lượng và kiểm soát chất lượng do chỉ làm việc với một nhà thầu.",
        ],
      },
      {
        heading: "Phương pháp quản lý dự án tiên tiến",
        body: [
          "Hợp tác chặt chẽ cùng Mori Construction (Nhật Bản), NICON ứng dụng quy trình BIM (Building Information Modeling) cho mọi giai đoạn.",
          "Đội ngũ chuyên viên BIM giàu kinh nghiệm cung cấp giải pháp đồng bộ, giúp chủ đầu tư có toàn bộ thông tin dự án và dự đoán rủi ro sớm.",
        ],
      },
      {
        heading: "Sản phẩm tốt nhất từ những con người tốt nhất",
        body: [
          "NICON sở hữu mạng lưới đối tác quản lý quốc tế trong các lĩnh vực kiến trúc, kết cấu, nội thất và M&E.",
          "Đội ngũ giàu kinh nghiệm gồm project manager, kiến trúc sư, kỹ sư và công nhân lành nghề có thể xử lý các dự án QUY MÔ LỚN – TIẾN ĐỘ GẤP – CHẤT LƯỢNG CAO.",
        ],
      },
    ],
    highlights: ["BIM 4D / 5D", "ISO 9001:2015", "150+ dự án D&B", "Mori Group partner"],
  },
  {
    slug: "main-contractor",
    shortTitle: "Main Contractor",
    title: "Dịch vụ Tổng thầu chính (Main Contractor)",
    tagline: "Quản lý trọn gói thi công — bàn giao chìa khóa trao tay.",
    intro:
      "Với vai trò Tổng thầu chính Việt – Nhật, NICON thực hiện đầy đủ các nhiệm vụ của một dự án xây dựng công nghiệp: thiết kế, xin phép và thi công, đảm bảo chủ đầu tư chỉ cần làm việc với một đầu mối duy nhất.",
    sections: [
      {
        heading: "Phạm vi công việc của Tổng thầu chính",
        body: [
          "Quản lý toàn bộ công trường, điều phối các nhà thầu phụ và nhà cung cấp.",
          "Đảm bảo tiến độ, chất lượng và an toàn lao động (HSE) tại công trường.",
          "Báo cáo định kỳ cho chủ đầu tư bằng tiếng Việt – Anh – Nhật.",
        ],
      },
      {
        heading: "Phương pháp quản lý chuẩn quốc tế",
        body: [
          "Áp dụng tiêu chuẩn quản lý dự án PMP và phương pháp Lean Construction.",
          "Sử dụng phần mềm MS Project, Primavera P6 cho lập tiến độ và kiểm soát chi phí.",
          "Quy trình QA/QC theo ISO 9001:2015 cho từng hạng mục thi công.",
        ],
      },
      {
        heading: "Đối tác chiến lược cùng Mori Group",
        body: [
          "Sự hợp tác cùng Mori Industry Group (Nhật Bản) mang đến tiêu chuẩn kỹ thuật và văn hóa làm việc chuẩn Nhật cho mọi dự án NICON đảm nhận.",
        ],
      },
    ],
    highlights: ["18+ năm kinh nghiệm", "Quản lý PMP", "QA/QC ISO 9001", "An toàn HSE chuẩn Nhật"],
  },
  {
    slug: "general-contractor",
    shortTitle: "General Contractor",
    title: "Dịch vụ Tổng thầu (General Contractor)",
    tagline: "Đảm nhận toàn bộ vòng đời thi công nhà máy công nghiệp.",
    intro:
      "Với cương vị Tổng thầu Việt Nam – Nhật Bản, NICON thực hiện đầy đủ nhiệm vụ của một dự án xây dựng công nghiệp gồm thiết kế, xin phép, thi công và bàn giao trọn gói. Phương thức D&B / EPC giúp chủ đầu tư yên tâm về tiến độ và chi phí.",
    sections: [
      {
        heading: "Vai trò Tổng thầu",
        body: [
          "Quản lý toàn diện từ thiết kế cơ sở, thiết kế kỹ thuật đến bản vẽ thi công.",
          "Mua sắm vật tư – thiết bị (Procurement) và quản lý chuỗi cung ứng cho dự án.",
          "Tổ chức thi công, nghiệm thu từng phần và bàn giao công trình hoàn chỉnh.",
        ],
      },
      {
        heading: "Năng lực mega-project",
        body: [
          "NICON đã thành công thực hiện các tổ hợp công nghiệp 250.000 m² như Lâm Hiệp Hưng – Tân Toàn Phát.",
          "Năng lực tổ chức công trường lớn với hàng trăm công nhân, thiết bị nặng và logistics phức tạp.",
        ],
      },
      {
        heading: "Cam kết chất lượng",
        body: [
          "100% công trình bàn giao đúng tiến độ trong 5 năm gần nhất.",
          "Bảo hành 24 tháng cho phần xây dựng, 12 tháng cho phần MEP.",
        ],
      },
    ],
    highlights: ["Mega-project 250.000m²", "Procurement chuyên nghiệp", "Bảo hành 24 tháng", "Đa quốc gia"],
  },
  {
    slug: "mep-contractor",
    shortTitle: "MEP Contractor",
    title: "Dịch vụ Tổng thầu MEP",
    tagline: "Hệ thống Cơ – Điện – Nước đồng bộ và tối ưu vận hành.",
    intro:
      "MEP (Mechanical – Electrical – Plumbing) là phần quan trọng quyết định hiệu quả vận hành nhà máy. NICON cung cấp dịch vụ tổng thầu MEP độc lập hoặc tích hợp trong gói D&B, với đội ngũ kỹ sư chuyên ngành giàu kinh nghiệm.",
    sections: [
      {
        heading: "Phạm vi MEP của NICON",
        body: [
          "Hệ thống điện công nghiệp: trung – hạ thế, máy phát dự phòng, UPS, hệ chiếu sáng năng lượng cao.",
          "Hệ HVAC, thông gió và phòng sạch theo cấp ISO Class 5/7/8.",
          "Hệ cấp – thoát nước, nước nóng năng lượng mặt trời, hệ xử lý nước thải.",
          "Hệ PCCC sprinkler, báo cháy địa chỉ theo TCVN và NFPA.",
        ],
      },
      {
        heading: "Tích hợp và bàn giao",
        body: [
          "Quy trình T&C (Testing & Commissioning) bài bản, có sự chứng kiến của tư vấn giám sát và chủ đầu tư.",
          "Bàn giao kèm hồ sơ As-built, sách hướng dẫn vận hành – bảo trì (O&M Manual).",
          "Đào tạo vận hành cho đội ngũ kỹ thuật của chủ đầu tư.",
        ],
      },
      {
        heading: "Quản lý dự án bằng BIM",
        body: [
          "Mô hình MEP 3D phát hiện xung đột hạng mục trước khi thi công, giảm 80% chỉnh sửa hiện trường.",
          "Tài liệu BIM bàn giao cho chủ đầu tư phục vụ vận hành – bảo trì lâu dài.",
        ],
      },
    ],
    highlights: ["BIM MEP 3D", "Phòng sạch ISO 5-8", "T&C chuyên nghiệp", "O&M training"],
  },
];

export const getServiceBySlug = (slug: string) => services.find((s) => s.slug === slug);
