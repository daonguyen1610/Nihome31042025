// News articles — sourced & adapted from nicon.vn/news
import type { LocalizedFields } from "@/lib/localize";

export type NewsItem = {
  id: string;
  date: string;
  img: string;
  category: string;
  title: string;
  excerpt: string;
  content: string[];
  /** Optional translations keyed by language code. */
  i18n?: LocalizedFields<NewsItem>;
};

/** Vietnamese category → translations. Used by listing filters and chips. */
export const newsCategoryI18n: Record<string, { en: string; zh: string; ja: string }> = {
  "Kỹ thuật": { en: "Technical", zh: "技术", ja: "技術" },
  "Thiết kế": { en: "Design", zh: "设计", ja: "設計" },
  "Tiêu chuẩn": { en: "Standards", zh: "标准", ja: "基準" },
  "Dịch vụ": { en: "Services", zh: "服务", ja: "サービス" },
  "Báo giá": { en: "Pricing", zh: "报价", ja: "見積もり" },
  "Đối tác": { en: "Partners", zh: "合作伙伴", ja: "パートナー" },
};

export const news: NewsItem[] = [
  {
    id: "modern-fire-protection-system",
    date: "13.01.2025",
    img: "https://nicon.vn/content/images/thumbs/0006256.jpeg",
    category: "Kỹ thuật",
    title: "Hệ thống phòng cháy hiện đại cho các nhà máy có nguy cơ cao — Giải pháp tối ưu từ NICON",
    excerpt:
      "Nhà máy ngành hoá chất, dệt may thường đối mặt với rủi ro cháy nổ cao. Trang bị hệ thống PCCC hiện đại là yếu tố then chốt bảo vệ tài sản và đảm bảo vận hành liên tục.",
    content: [
      "Nhà máy trong các ngành công nghiệp hóa chất, dệt may, may mặc thường đối mặt với nguy cơ cháy nổ cao do sự hiện diện của vật liệu dễ cháy, thiết bị sinh nhiệt lớn và môi trường sản xuất phức tạp.",
      "NICON cung cấp giải pháp PCCC trọn gói: thiết kế hệ thống sprinkler, hệ báo cháy địa chỉ, bình CO2/foam, vách ngăn chống cháy theo tiêu chuẩn TCVN 3890:2009 và NFPA quốc tế.",
      "Đội ngũ kỹ sư phòng cháy của NICON có chứng chỉ hành nghề, đảm bảo hồ sơ thẩm duyệt nhanh gọn và bàn giao nghiệm thu thuận lợi.",
    ],
    i18n: {
      en: {
        title: "Modern fire protection systems for high-risk factories — NICON's optimal solution",
        excerpt:
          "Chemical and textile factories often face elevated fire risk. Equipping a modern fire protection system is essential to safeguard assets and ensure continuous operation.",
        content: [
          "Factories in chemical, textile and apparel industries face high fire risks due to flammable materials, heat-generating equipment and complex production environments.",
          "NICON delivers turnkey fire protection: sprinkler design, addressable fire alarms, CO2/foam suppression and fire-rated partitions per TCVN 3890:2009 and international NFPA standards.",
          "Our certified fire safety engineers ensure smooth approval submissions and acceptance handover.",
        ],
      },
      zh: {
        title: "高风险工厂的现代消防系统 — NICON 的最佳解决方案",
        excerpt:
          "化工与纺织工厂面临较高的火灾风险。配置现代消防系统是保护资产并确保连续运营的关键。",
        content: [
          "化工、纺织与服装行业的工厂由于易燃材料、产热设备和复杂的生产环境,面临较高的火灾风险。",
          "NICON 提供全包消防解决方案:喷淋系统设计、地址型火警、CO2/泡沫灭火及防火隔断,符合TCVN 3890:2009与国际NFPA标准。",
          "NICON 持证的消防工程师团队确保审批快速通过、验收顺利交付。",
        ],
      },
      ja: {
        title: "高リスク工場向けの最新消防システム — NICONの最適ソリューション",
        excerpt:
          "化学・繊維工場は火災リスクが高く、最新の消防システムは資産保護と継続稼働の鍵です。",
        content: [
          "化学・繊維・縫製業の工場は、可燃物・発熱機器・複雑な生産環境により火災リスクが高くなります。",
          "NICONはスプリンクラー設計、アドレッサブル火災報知、CO2/フォーム消火、防火区画をTCVN 3890:2009および国際NFPA基準で一括提供します。",
          "有資格の消防エンジニアが審査・引渡しまでスムーズに対応します。",
        ],
      },
    },
  },
  {
    id: "build-concept-for-design-project",
    date: "25.12.2024",
    img: "https://nicon.vn/content/images/thumbs/0006251.jpeg",
    category: "Thiết kế",
    title: "Xây dựng concept cho dự án thiết kế",
    excerpt:
      "Tạo concept cho một dự án thiết kế nhà ở là bước đầu tiên nhưng quan trọng, giúp định hình ý tưởng và đảm bảo sự hài hoà giữa thẩm mỹ, công năng và nhu cầu thực tế.",
    content: [
      "Concept thiết kế là nền tảng định hướng cho toàn bộ quá trình thi công. Một concept tốt phản ánh cá tính và câu chuyện riêng của ngôi nhà.",
      "NICON xây dựng concept dựa trên ba trụ cột: nghiên cứu khách hàng – tham chiếu xu hướng – thử nghiệm vật liệu. Mỗi dự án đều có moodboard riêng được trình bày 3D trước khi triển khai chi tiết.",
      "Thông qua quy trình này, chúng tôi đảm bảo khách hàng nhìn thấy rõ kết quả cuối cùng trước khi xây dựng, tránh phát sinh chỉnh sửa tốn kém.",
    ],
    i18n: {
      en: {
        title: "Building the concept for a design project",
        excerpt:
          "Creating the concept for a residential project is the first and crucial step that shapes ideas and harmonizes aesthetics, function and real needs.",
        content: [
          "Design concept is the foundation guiding the entire construction process. A great concept reflects the personality and story of the home.",
          "NICON builds concepts on three pillars: client research, trend reference and material experimentation. Every project gets a 3D moodboard before detailed development.",
          "This process ensures clients see the final outcome before construction, avoiding costly revisions.",
        ],
      },
      zh: {
        title: "为设计项目构建概念",
        excerpt:
          "为住宅设计项目构建概念是第一步也是关键一步,帮助塑造创意并平衡美学、功能与实际需求。",
        content: [
          "设计概念是整个施工过程的基础。优秀的概念反映出住宅的个性与故事。",
          "NICON 基于三大支柱构建概念:客户研究、趋势参考、材料实验。每个项目都会在细化前提供3D情绪板。",
          "通过此流程,客户在施工前即可清晰看到最终效果,避免代价高昂的返工。",
        ],
      },
      ja: {
        title: "デザインプロジェクトのコンセプト構築",
        excerpt:
          "住宅プロジェクトのコンセプト作りは最初で重要なステップであり、美学・機能・実需要の調和を形作ります。",
        content: [
          "デザインコンセプトは施工全体の基盤です。良いコンセプトは住まいの個性と物語を映し出します。",
          "NICONはクライアント調査・トレンド参照・素材実験の3本柱でコンセプトを構築。各案件で詳細展開前に3Dムードボードを提示します。",
          "施工前に最終形をご確認いただくことで、コスト高な手戻りを回避します。",
        ],
      },
    },
  },
  {
    id: "gmp-standards-for-factories",
    date: "16.12.2024",
    img: "https://nicon.vn/content/images/thumbs/0006243.jpeg",
    category: "Tiêu chuẩn",
    title: "Tìm hiểu chuẩn GMP trong nhà máy thực phẩm, dược phẩm và mỹ phẩm",
    excerpt:
      "Trong các ngành được kiểm soát chặt chẽ như thực phẩm, dược phẩm và mỹ phẩm, việc tuân thủ GMP là điều kiện tiên quyết để đảm bảo chất lượng và an toàn sản phẩm.",
    content: [
      "GMP (Good Manufacturing Practices) là hệ tiêu chuẩn bắt buộc cho ngành thực phẩm – dược – mỹ phẩm, kiểm soát toàn diện từ thiết kế nhà xưởng đến quy trình sản xuất.",
      "NICON đã thiết kế và thi công nhiều dự án đạt GMP-WHO, GMP-EU như nhà máy dược, mỹ phẩm. Các yêu cầu cốt lõi gồm: phân khu sạch theo cấp ISO 5/7/8, hệ HVAC độc lập, vật liệu không phát bụi và quy trình một chiều.",
      "Đội ngũ kỹ sư NICON tư vấn miễn phí giai đoạn lập dự án, giúp chủ đầu tư tránh sai sót thiết kế dẫn đến không đạt khi thẩm định.",
    ],
    i18n: {
      en: {
        title: "Understanding GMP standards in food, pharma and cosmetics factories",
        excerpt:
          "In tightly regulated industries like food, pharma and cosmetics, GMP compliance is a prerequisite to guarantee product quality and safety.",
        content: [
          "GMP (Good Manufacturing Practices) is mandatory for food, pharma and cosmetics, controlling everything from facility design to production processes.",
          "NICON has delivered many GMP-WHO and GMP-EU projects. Core requirements include ISO 5/7/8 clean zones, independent HVAC, dust-free materials and one-way flow.",
          "Our engineers offer free design consultation to help investors avoid mistakes that fail audits.",
        ],
      },
      zh: {
        title: "了解食品、制药与化妆品工厂的GMP标准",
        excerpt:
          "在食品、制药与化妆品等严格监管行业,GMP 合规是确保产品质量与安全的前提条件。",
        content: [
          "GMP(良好生产规范)是食品、制药与化妆品行业的强制标准,从厂房设计到生产流程全面管控。",
          "NICON 已完成多个达到 GMP-WHO、GMP-EU 标准的项目。核心要求包括ISO 5/7/8洁净分区、独立HVAC、无尘材料及单向流程。",
          "NICON 工程师团队在立项阶段提供免费咨询,帮助投资方避免审核失败的设计缺陷。",
        ],
      },
      ja: {
        title: "食品・医薬・化粧品工場におけるGMP基準を学ぶ",
        excerpt:
          "厳しく規制される食品・医薬・化粧品分野では、GMP遵守が品質と安全を保証する前提条件です。",
        content: [
          "GMP(適正製造規範)は食品・医薬・化粧品で必須の基準であり、施設設計から生産工程まで全面的に管理します。",
          "NICONはGMP-WHO・GMP-EU適合の多くの案件を実績しています。要件はISO 5/7/8クリーンゾーン、独立HVAC、無塵材料、一方向動線など。",
          "投資家が審査不適合となる設計ミスを避けられるよう、計画段階で無料相談を行います。",
        ],
      },
    },
  },
  {
    id: "nicon-consulting-factory-projects",
    date: "15.12.2024",
    img: "https://nicon.vn/content/images/thumbs/0006242.jpeg",
    category: "Dịch vụ",
    title: "NICON tư vấn và thiết kế dự án nhà máy, nhà xưởng",
    excerpt:
      "Với nhiều năm kinh nghiệm tư vấn và thiết kế, NICON cung cấp dịch vụ trọn gói từ tư vấn thiết kế đến thi công, luôn đem lại giải pháp tối ưu cho dự án của nhà đầu tư.",
    content: [
      "Là nhà thầu chuyên nghiệp với hơn 18 năm kinh nghiệm, NICON đã đồng hành cùng hơn 80 chủ đầu tư trong và ngoài nước trong việc tư vấn và thiết kế nhà máy, nhà xưởng công nghiệp.",
      "Dịch vụ tư vấn của NICON bao gồm: lựa chọn địa điểm, lập dự án đầu tư, thiết kế cơ sở, thiết kế kỹ thuật và bản vẽ thi công.",
      "Chúng tôi cam kết bàn giao hồ sơ đúng tiến độ và hỗ trợ chủ đầu tư trong suốt quá trình xin phép xây dựng.",
    ],
    i18n: {
      en: {
        title: "NICON consults and designs factory and workshop projects",
        excerpt:
          "With years of consulting and design experience, NICON provides turnkey services from design consultation to construction, delivering optimal solutions for investors.",
        content: [
          "As a professional contractor with 18+ years of experience, NICON has partnered with 80+ domestic and international investors on factory and workshop consulting and design.",
          "Our consulting covers: site selection, investment project setup, basic design, detailed engineering and shop drawings.",
          "We commit to on-schedule delivery and support investors throughout the construction permitting process.",
        ],
      },
      zh: {
        title: "NICON 工厂、车间项目咨询与设计",
        excerpt:
          "凭借多年咨询设计经验,NICON 提供从设计咨询到施工的全包服务,为投资方带来最佳方案。",
        content: [
          "作为拥有18年以上经验的专业承包商,NICON 已为80多家国内外投资方提供工业厂房咨询与设计服务。",
          "咨询服务涵盖:选址、立项、基础设计、技术设计与施工图。",
          "我们承诺按期交付并在施工许可全过程中支持投资方。",
        ],
      },
      ja: {
        title: "NICONによる工場・倉庫プロジェクトのコンサルティングと設計",
        excerpt:
          "長年のコンサルと設計経験を活かし、NICONは設計コンサルから施工まで一括サービスを提供。投資家に最適な解決策をお届けします。",
        content: [
          "18年以上の経験を持つプロフェッショナルな施工会社として、NICONは80社超の国内外投資家と工場・倉庫のコンサル・設計に携わりました。",
          "コンサル業務には用地選定、投資計画策定、基本設計、実施設計、施工図が含まれます。",
          "期日通りの納品と建築許可取得まで投資家をサポートします。",
        ],
      },
    },
  },
  {
    id: "nicon-factory-price-list-2024",
    date: "10.11.2024",
    img: "https://nicon.vn/content/images/thumbs/0006239.jpeg",
    category: "Báo giá",
    title: "Bảng giá xây dựng nhà máy NICON 2024",
    excerpt:
      "NICON cập nhật bảng giá xây dựng nhà máy nhà xưởng năm 2024 — minh bạch theo từng loại kết cấu và quy mô diện tích.",
    content: [
      "Bảng giá năm 2024 của NICON áp dụng cho các loại nhà xưởng kết cấu thép tiền chế và bê tông cốt thép, với các mức từ tiêu chuẩn đến cao cấp.",
      "Mức giá tham khảo: nhà xưởng thép tiền chế từ 2.700.000 đ/m², nhà xưởng có lửng văn phòng từ 3.500.000 đ/m², kho lạnh từ 5.000.000 đ/m² (chưa VAT).",
      "Để có báo giá chính xác, vui lòng liên hệ phòng kinh doanh NICON với thông tin chi tiết về địa điểm, diện tích và yêu cầu kỹ thuật cụ thể.",
    ],
    i18n: {
      en: {
        title: "NICON 2024 factory construction price list",
        excerpt:
          "NICON's updated 2024 price list for factory construction — transparent by structure type and area scale.",
        content: [
          "NICON's 2024 price list covers prefabricated steel and reinforced concrete factories, from standard to premium tiers.",
          "Reference prices: pre-engineered steel factory from 2,700,000 VND/m²; factory with office mezzanine from 3,500,000 VND/m²; cold storage from 5,000,000 VND/m² (excl. VAT).",
          "For an exact quote, contact NICON's sales team with location, area and technical requirements.",
        ],
      },
      zh: {
        title: "NICON 2024 年工厂建造报价单",
        excerpt:
          "NICON 更新2024年厂房建造报价 — 按结构类型与面积规模透明定价。",
        content: [
          "2024年报价适用于预制钢结构与钢筋混凝土厂房,涵盖标准到高端等级。",
          "参考价:预制钢结构厂房自2,700,000越南盾/㎡;带办公夹层厂房自3,500,000越南盾/㎡;冷库自5,000,000越南盾/㎡(不含增值税)。",
          "如需精准报价,请联系NICON销售部门并提供选址、面积及技术要求详情。",
        ],
      },
      ja: {
        title: "NICON 2024年 工場建設価格表",
        excerpt:
          "NICONの2024年工場建設価格表を更新 — 構造種別と面積規模ごとに透明に提示します。",
        content: [
          "2024年価格表はプレハブ鉄骨およびRC造工場を対象とし、スタンダードからプレミアムまで網羅します。",
          "参考価格:プレハブ鉄骨工場 2,700,000 VND/㎡〜、オフィス中2階付工場 3,500,000 VND/㎡〜、冷蔵倉庫 5,000,000 VND/㎡〜(税抜)。",
          "正確な見積もりは、所在地・面積・技術要件をNICON営業部までお知らせください。",
        ],
      },
    },
  },
  {
    id: "nihome-redefining-service-apartment",
    date: "07.11.2024",
    img: "https://nicon.vn/content/images/thumbs/0006235.png",
    category: "Đối tác",
    title: "NIHOME — \"Định nghĩa lại\" mô hình căn hộ dịch vụ",
    excerpt:
      "Nicon và Nihome đã \"định nghĩa lại\" khái niệm căn hộ dịch vụ thành một không gian sống mang đến trải nghiệm thư giãn, tận hưởng cho khách hàng.",
    content: [
      "NIHOME là thương hiệu căn hộ dịch vụ cao cấp được phát triển bởi NICON, hướng đến đối tượng chuyên gia nước ngoài làm việc dài hạn tại Việt Nam.",
      "Khác biệt của NIHOME là sự kết hợp giữa thiết kế nội thất tối giản kiểu Nhật, dịch vụ khách sạn 4 sao và vị trí trung tâm các đô thị lớn.",
      "Hiện NIHOME đã có mặt tại Thủ Đức, Bình Dương và đang mở rộng sang Hà Nội trong giai đoạn 2025-2026.",
    ],
    i18n: {
      en: {
        title: "NIHOME — Redefining the serviced apartment model",
        excerpt:
          "Nicon and Nihome have redefined serviced apartments into a living space that delivers a relaxing, enjoyable experience for residents.",
        content: [
          "NIHOME is a premium serviced apartment brand developed by NICON, targeting foreign experts on long-term assignments in Vietnam.",
          "NIHOME stands out by blending Japanese-inspired minimalist interiors, 4-star hotel service and prime urban locations.",
          "NIHOME currently operates in Thu Duc and Binh Duong and is expanding to Hanoi during 2025-2026.",
        ],
      },
      zh: {
        title: "NIHOME — 重新定义服务公寓模式",
        excerpt:
          "Nicon 与 Nihome 重新定义了服务公寓的概念,打造为住客带来放松与享受体验的居住空间。",
        content: [
          "NIHOME 是 NICON 打造的高端服务公寓品牌,面向在越南长期工作的外籍专家。",
          "NIHOME 的差异化在于将日式极简内饰、四星级酒店服务与大城市核心地段相结合。",
          "目前 NIHOME 已落地守德区与平阳省,并将在2025-2026年扩展至河内。",
        ],
      },
      ja: {
        title: "NIHOME — サービスアパートメントを再定義",
        excerpt:
          "NiconとNihomeはサービスアパートメントを再定義し、入居者に寛ぎと愉しみを届ける生活空間を創出しました。",
        content: [
          "NIHOMEはNICONが展開する高級サービスアパートメントブランドで、ベトナム長期駐在の外国人専門家を対象としています。",
          "和風ミニマリズムの内装、4つ星ホテル品質のサービス、大都市中心の立地を融合した点が特長です。",
          "現在、トゥドゥック・ビンズンで展開中で、2025-2026年にハノイへ拡大予定です。",
        ],
      },
    },
  },
];

export const getNewsById = (id: string) => news.find((n) => n.id === id);
