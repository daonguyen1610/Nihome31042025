import ceremony from "@/assets/activity-ceremony.jpg";
import handover from "@/assets/activity-handover.jpg";
import opening from "@/assets/activity-opening.jpg";
import type { LocalizedFields } from "@/lib/localize";

export type Activity = {
  id: string;
  date: string;
  img: string;
  category: string;
  title: string;
  excerpt: string;
  content: string[];
  author?: string;
  /** Optional translations keyed by language code. */
  i18n?: LocalizedFields<Activity>;
};

/** Vietnamese category → translations. Used by listing filters and chips. */
export const activityCategoryI18n: Record<string, { en: string; zh: string; ja: string }> = {
  "Dịch vụ": { en: "Services", zh: "服务", ja: "サービス" },
  "Sự kiện": { en: "Events", zh: "活动", ja: "イベント" },
  "Khánh thành": { en: "Inauguration", zh: "落成典礼", ja: "竣工式" },
  "Triển lãm": { en: "Exhibitions", zh: "展览", ja: "展示会" },
  "Khởi công": { en: "Groundbreaking", zh: "奠基仪式", ja: "起工式" },
};

export const activities: Activity[] = [
  {
    id: "dich-vu-xay-dung-tron-goi-nicon",
    date: "15.12.2025",
    img: handover,
    category: "Dịch vụ",
    author: "NICON Editorial",
    title: "Dịch vụ xây dựng trọn gói NICON — Giải pháp tối ưu cho công trình nhà xưởng, nhà máy và dự án dân dụng",
    excerpt:
      "Xây dựng trọn gói là hình thức chủ đầu tư giao toàn bộ quá trình xây nhà cho một đơn vị chuyên nghiệp. Từ khảo sát, lập dự toán, thiết kế, xin phép đến chuẩn bị vật tư, thi công hoàn thiện và bàn giao nhà.",
    content: [
      "Trong bối cảnh thị trường xây dựng ngày càng cạnh tranh, các chủ đầu tư mong muốn rút ngắn thời gian triển khai, kiểm soát chi phí và đảm bảo chất lượng đồng bộ. Dịch vụ xây dựng trọn gói (Design & Build) ra đời nhằm giải quyết toàn diện những yêu cầu đó.",
      "Tại NICON, chúng tôi đảm nhiệm toàn bộ chuỗi giá trị: từ tư vấn ý tưởng, thiết kế kiến trúc – kết cấu – MEP, xin phép xây dựng, lựa chọn vật tư, đến thi công và bàn giao. Một đầu mối duy nhất chịu trách nhiệm toàn bộ dự án giúp chủ đầu tư tiết kiệm thời gian quản lý và giảm thiểu rủi ro phát sinh.",
      "Quy trình quản lý dự án theo chuẩn ISO 9001:2015 đảm bảo mọi giai đoạn đều được kiểm soát chất lượng nghiêm ngặt. Đội ngũ kỹ sư trên 20 năm kinh nghiệm trong lĩnh vực nhà máy, nhà xưởng công nghiệp và công trình dân dụng cam kết mang đến giải pháp tối ưu nhất cho mỗi loại hình công trình.",
      "Với hơn 150 dự án đã hoàn thành tại các khu công nghiệp lớn như VSIP, Hựu Thạnh, Giang Điền, Long An..., NICON tự tin là đối tác tin cậy cho hành trình phát triển của doanh nghiệp bạn.",
    ],
    i18n: {
      en: {
        title: "NICON turnkey construction — Optimal solution for workshops, factories and civil projects",
        excerpt:
          "Turnkey construction is when investors entrust the entire build process to one professional partner — from survey, estimate, design and permitting to materials, construction and handover.",
        content: [
          "In an increasingly competitive construction market, investors want shorter delivery, controlled cost and consistent quality. Design & Build (turnkey) was born to address exactly that.",
          "At NICON, we handle the entire value chain: concept consulting, architecture/structure/MEP design, permitting, material selection, construction and handover. A single point of responsibility saves management time and reduces risk.",
          "ISO 9001:2015 project management ensures rigorous quality control at every stage. Our engineers, with over 20 years in industrial and civil works, deliver the optimal solution for every project type.",
          "With 150+ projects delivered in major IPs like VSIP, Huu Thanh, Giang Dien and Long An, NICON is confident to be a trusted partner for your business growth.",
        ],
      },
      zh: {
        title: "NICON 全包施工 — 厂房、工厂与民用项目的最佳解决方案",
        excerpt:
          "全包施工是指投资方将整个建造过程交给一家专业单位 — 从勘察、预算、设计、许可到材料、施工与交付。",
        content: [
          "在日益激烈的建筑市场中,投资方希望缩短交付时间、控制成本并保持质量一致。设计施工一体化(全包)正是为此而生。",
          "在 NICON,我们承担整个价值链:概念咨询、建筑/结构/MEP 设计、许可、材料选择、施工与交付。单一责任主体节省管理时间、降低风险。",
          "ISO 9001:2015 项目管理确保各阶段严格质量控制。20年以上经验的工程师团队为每类项目提供最佳方案。",
          "在VSIP、Huu Thanh、Giang Dien、Long An等主要工业园已完成150+项目,NICON 是您企业发展的可信合作伙伴。",
        ],
      },
      ja: {
        title: "NICONの一括施工 — 倉庫・工場・民間プロジェクトの最適ソリューション",
        excerpt:
          "一括施工は投資家が建設プロセス全体をプロフェッショナル1社に委ねる方式です — 調査・見積・設計・許可から資材・施工・引渡しまで。",
        content: [
          "競争が激化する建設市場において、投資家は納期短縮・コスト管理・品質の一貫性を求めます。Design & Build(一括)はそれに応える方式です。",
          "NICONは構想・建築/構造/MEP設計・許可・資材選定・施工・引渡しまで全工程を担います。窓口一本化で管理時間とリスクを削減します。",
          "ISO 9001:2015のプロジェクト管理により各段階で厳格な品質管理を実施。20年超の経験を持つエンジニアが最適解をご提供します。",
          "VSIP・Huu Thanh・Giang Dien・Long Anなど主要工業団地で150件超の実績があり、NICONは事業成長の信頼できるパートナーです。",
        ],
      },
    },
  },
  {
    id: "khoi-cong-bma-tay-ninh",
    date: "08.11.2025",
    img: ceremony,
    category: "Sự kiện",
    author: "Phòng Truyền Thông",
    title: "Khởi công Dự án Nhà máy Bảo Minh Ân Việt Nam tại KCN Hựu Thạnh, Tây Ninh",
    excerpt:
      "Ngày 19/10/2025, NICON tự hào chính thức khởi công Dự án Nhà máy Bảo Minh Ân Việt Nam tại KCN Hựu Thạnh, xã Đức Hòa, tỉnh Tây Ninh.",
    content: [
      "Sáng ngày 19/10/2025, lễ khởi công Dự án Nhà máy Bảo Minh Ân Việt Nam đã diễn ra trang trọng tại Khu công nghiệp Hựu Thạnh, xã Đức Hòa, tỉnh Tây Ninh. Dự án đánh dấu bước phát triển mới trong hợp tác chiến lược giữa NICON và tập đoàn Bảo Minh Ân.",
      "Buổi lễ có sự tham dự của đại diện lãnh đạo địa phương, ban giám đốc chủ đầu tư cùng đội ngũ kỹ sư, công nhân của NICON. Phát biểu tại sự kiện, đại diện chủ đầu tư bày tỏ tin tưởng vào năng lực thi công và quản lý dự án của NICON.",
      "Nhà máy được thiết kế trên diện tích 15.000 m² với hệ thống dây chuyền sản xuất hiện đại, tuân thủ tiêu chuẩn quốc tế về môi trường và an toàn lao động. Dự kiến hoàn thành và đưa vào vận hành vào quý IV/2026.",
    ],
    i18n: {
      en: {
        title: "Groundbreaking of Bao Minh An Vietnam factory at Huu Thanh IP, Tay Ninh",
        excerpt:
          "On October 19, 2025, NICON proudly broke ground for the Bao Minh An Vietnam factory at Huu Thanh Industrial Park, Duc Hoa, Tay Ninh.",
        content: [
          "On the morning of October 19, 2025, the groundbreaking ceremony took place at Huu Thanh IP, marking a new milestone in the strategic partnership between NICON and the Bao Minh An Group.",
          "The ceremony was attended by local authorities, the investor's board and NICON's engineering and construction team. The investor expressed confidence in NICON's construction and project management capabilities.",
          "The 15,000 m² factory features modern production lines and complies with international environmental and safety standards. Completion and commissioning are scheduled for Q4 2026.",
        ],
      },
      zh: {
        title: "Bao Minh An 越南工厂在西宁省 Huu Thanh 工业园奠基",
        excerpt:
          "2025年10月19日,NICON 自豪地正式启动 Bao Minh An 越南工厂项目,位于西宁省德和县 Huu Thanh 工业园。",
        content: [
          "2025年10月19日上午,Bao Minh An 越南工厂奠基仪式在 Huu Thanh 工业园隆重举行,标志着 NICON 与 Bao Minh An 集团战略合作的新里程碑。",
          "出席仪式的有地方领导、投资方董事会及 NICON 的工程与施工团队。投资方代表对 NICON 的施工与项目管理能力表示信任。",
          "工厂占地15,000㎡,配备现代化生产线,符合国际环境与安全标准,预计将于2026年第四季度建成投产。",
        ],
      },
      ja: {
        title: "タイニン省フウタイン工業団地でBao Minh Anベトナム工場が起工",
        excerpt:
          "2025年10月19日、NICONはタイニン省ドゥックホア県フウタイン工業団地でBao Minh Anベトナム工場プロジェクトの起工式を執り行いました。",
        content: [
          "2025年10月19日午前、フウタイン工業団地で起工式が荘厳に行われ、NICONとBao Minh Anグループの戦略的パートナーシップにおける新たな節目となりました。",
          "式典には地方行政、投資家経営陣、NICONのエンジニア・施工チームが出席。投資家はNICONの施工と案件管理能力に信頼を表明しました。",
          "工場面積は15,000㎡、最新の生産ラインを備え、国際的な環境・労働安全基準に準拠。2026年第4四半期の完成・稼働を予定しています。",
        ],
      },
    },
  },
  {
    id: "grand-opening-trimas",
    date: "08.11.2025",
    img: opening,
    category: "Khánh thành",
    author: "Phòng Truyền Thông",
    title: "Grand Opening — Nhà máy TriMas",
    excerpt:
      "Ngày 13/10/2025 vừa qua, Nhà máy TriMas tại KCN VSIP IIA mở rộng, Phường Vĩnh Tân, TP. Hồ Chí Minh chính thức khánh thành trong không khí hân hoan và đầy phấn khởi.",
    content: [
      "Ngày 13/10/2025, lễ khánh thành Nhà máy TriMas đã diễn ra trong không khí trang trọng và hân hoan tại KCN VSIP IIA mở rộng, Phường Vĩnh Tân, TP. Hồ Chí Minh.",
      "Công trình do NICON đảm nhận vai trò Tổng thầu Thiết kế – Thi công với diện tích 10.000 m², hoàn thành trong 11 tháng đúng tiến độ cam kết. Đây là dấu mốc quan trọng trong quan hệ hợp tác lâu dài giữa NICON và tập đoàn TriMas.",
      "Nhà máy được trang bị các hệ thống tiên tiến nhất về sản xuất, MEP và xử lý môi trường, đáp ứng tiêu chuẩn LEED Silver.",
    ],
    i18n: {
      en: {
        title: "Grand Opening — TriMas Factory",
        excerpt:
          "On October 13, 2025, the TriMas factory at VSIP IIA Expansion, Vinh Tan Ward, HCMC was officially inaugurated in a joyful atmosphere.",
        content: [
          "On October 13, 2025, the inauguration ceremony took place at VSIP IIA Expansion, Vinh Tan Ward, HCMC.",
          "Built by NICON as Design–Build general contractor across 10,000 m², the project was completed in 11 months on schedule — a key milestone in the long-term partnership between NICON and TriMas.",
          "The factory is equipped with cutting-edge production, MEP and environmental treatment systems meeting LEED Silver standards.",
        ],
      },
      zh: {
        title: "盛大落成 — TriMas 工厂",
        excerpt:
          "2025年10月13日,位于胡志明市永新坊 VSIP IIA 扩展区的 TriMas 工厂在欢庆氛围中正式落成。",
        content: [
          "2025年10月13日,TriMas 工厂落成典礼在 VSIP IIA 扩展区隆重举行。",
          "项目由 NICON 担任设计施工总承包,占地10,000㎡,11个月按期完工,是 NICON 与 TriMas 集团长期合作的重要里程碑。",
          "工厂配备最先进的生产、MEP 与环境处理系统,达到 LEED Silver 标准。",
        ],
      },
      ja: {
        title: "竣工 — TriMas工場",
        excerpt:
          "2025年10月13日、ホーチミン市ヴィンタン区VSIP IIA拡張地区のTriMas工場が華やかな雰囲気の中、正式に竣工しました。",
        content: [
          "2025年10月13日、VSIP IIA拡張地区で竣工式が荘厳に行われました。",
          "NICONが設計・施工一括の総合請負業者として10,000㎡を担当し、11ヶ月で予定通り完成。NICONとTriMasの長期的な連携における重要な節目となりました。",
          "工場は最先端の生産・MEP・環境処理システムを備え、LEED Silver基準を満たしています。",
        ],
      },
    },
  },
  {
    id: "nicon-trien-lam-pccc-2024",
    date: "17.08.2024",
    img: ceremony,
    category: "Triển lãm",
    author: "Phòng Marketing",
    title: "NICON tại Triển Lãm Quốc Tế Về Kỹ Thuật, Thiết Bị An Toàn, Bảo Vệ, Phòng Cháy Chữa Cháy Lần Thứ 17",
    excerpt:
      "NICON tham gia triển lãm quốc tế nhằm cập nhật những công nghệ mới nhất trong lĩnh vực an toàn và phòng cháy chữa cháy.",
    content: [
      "NICON vinh dự góp mặt tại Triển lãm Quốc tế lần thứ 17 về kỹ thuật, thiết bị an toàn, bảo vệ và phòng cháy chữa cháy diễn ra tại Trung tâm Hội chợ Triển lãm Sài Gòn (SECC).",
      "Đây là cơ hội để NICON tiếp cận những công nghệ PCCC mới nhất, nâng cao năng lực thiết kế và thi công hệ thống an toàn cho các nhà máy, nhà xưởng — một trong những tiêu chí then chốt khi triển khai dự án công nghiệp.",
      "Đoàn kỹ sư NICON đã có nhiều buổi trao đổi chuyên môn với các nhà cung cấp giải pháp hàng đầu thế giới đến từ Nhật Bản, Đức và Hoa Kỳ.",
    ],
    i18n: {
      en: {
        title: "NICON at the 17th International Exhibition on Safety, Security & Fire Protection",
        excerpt:
          "NICON joined the international exhibition to update the latest technologies in safety and fire protection.",
        content: [
          "NICON proudly attended the 17th International Exhibition on safety, security and fire protection at SECC, Saigon.",
          "This was an opportunity to access the latest fire protection technology and enhance NICON's design and installation capabilities for factory safety systems — a critical criterion in industrial projects.",
          "Our engineering team had multiple technical exchanges with leading global suppliers from Japan, Germany and the US.",
        ],
      },
      zh: {
        title: "NICON 参加第17届国际安全、保护及消防设备技术展览会",
        excerpt:
          "NICON 参加国际展览会,更新安全与消防领域的最新技术。",
        content: [
          "NICON 荣幸参加在西贡会展中心(SECC)举办的第17届国际安全、保护与消防设备技术展览会。",
          "此次展会让 NICON 接触最新消防技术,提升工厂安全系统设计与施工能力 — 这是工业项目的关键标准之一。",
          "工程师团队与来自日本、德国和美国的全球领先方案供应商进行了多场技术交流。",
        ],
      },
      ja: {
        title: "NICON、第17回国際安全・防護・消防機器技術展に出展",
        excerpt:
          "NICONは国際展示会に参加し、安全・消防分野の最新技術を学びました。",
        content: [
          "NICONはサイゴン展示センター(SECC)で開催された第17回国際安全・防護・消防機器技術展に参加しました。",
          "最新の消防技術を取り入れ、工場安全システムの設計・施工能力を強化する機会となりました — これは工業案件の重要な基準のひとつです。",
          "エンジニアチームは日本・ドイツ・米国の世界的トップサプライヤーと多くの技術交流を行いました。",
        ],
      },
    },
  },
  {
    id: "sinh-vien-vgu-tham-quan-stfm",
    date: "05.08.2022",
    img: opening,
    category: "Sự kiện",
    author: "Phòng Hành Chính",
    title: "Sinh viên trường Đại Học Quốc Tế Việt Đức tham quan công trình STFM",
    excerpt:
      "Ngày 04/08/2022, Công ty NICON đã có buổi làm việc, đón tiếp các bạn sinh viên năm 3 ngành Kiến trúc Trường Đại học Quốc tế Việt Đức (VGU).",
    content: [
      "Ngày 04/08/2022, NICON vinh hạnh đón tiếp đoàn sinh viên năm 3 ngành Kiến trúc Trường Đại học Quốc tế Việt Đức (VGU) tham quan công trình S.T.Food Marketing tại KCN VSIP II-A.",
      "Buổi tham quan giúp các bạn sinh viên có cái nhìn thực tế về quy trình thi công nhà máy công nghiệp, từ kết cấu thép, hệ MEP đến hoàn thiện. Đội ngũ kỹ sư NICON đã trực tiếp giới thiệu, giải đáp các thắc mắc chuyên môn.",
      "Hoạt động thuộc chương trình hợp tác giữa NICON và VGU nhằm góp phần đào tạo thế hệ kiến trúc sư – kỹ sư tương lai.",
    ],
    i18n: {
      en: {
        title: "VGU Architecture students visit the STFM construction site",
        excerpt:
          "On August 4, 2022, NICON hosted third-year Architecture students from Vietnamese-German University (VGU).",
        content: [
          "On August 4, 2022, NICON welcomed third-year Architecture students from VGU to tour the S.T.Food Marketing project at VSIP II-A IP.",
          "The visit gave students a real-world view of industrial factory construction — from steel structure and MEP to finishing. NICON engineers introduced and answered technical questions in person.",
          "This activity is part of the NICON–VGU partnership to nurture the next generation of architects and engineers.",
        ],
      },
      zh: {
        title: "越德国际大学学生参观 STFM 工程",
        excerpt:
          "2022年8月4日,NICON 公司接待越德国际大学(VGU)建筑专业三年级学生。",
        content: [
          "2022年8月4日,NICON 接待 VGU 建筑专业三年级学生参观位于 VSIP II-A 工业园的 S.T.Food Marketing 工程。",
          "此次参观让学生从钢结构、MEP 系统到精装,直观了解工业工厂施工流程。NICON 工程师团队亲自介绍并解答专业问题。",
          "活动属于 NICON 与 VGU 合作计划的一部分,旨在培养未来的建筑师与工程师。",
        ],
      },
      ja: {
        title: "ベトナム-ドイツ大学(VGU)建築学科の学生がSTFM現場を見学",
        excerpt:
          "2022年8月4日、NICONはVGU建築学科の3年生を迎え入れました。",
        content: [
          "2022年8月4日、NICONはVGU建築学科の3年生を迎え、VSIP II-A工業団地のS.T.Food Marketing現場を案内しました。",
          "見学を通じて学生は鉄骨・MEP・仕上げまで工業工場施工の実際を学び、NICONエンジニアが直接解説と質疑応答を行いました。",
          "この活動はNICONとVGUの提携プログラムの一環で、次世代の建築家・エンジニア育成に貢献しています。",
        ],
      },
    },
  },
  {
    id: "khoi-cong-stfm-2021",
    date: "12.06.2021",
    img: handover,
    category: "Khởi công",
    author: "Phòng Truyền Thông",
    title: "Lễ khởi công dự án Nhà máy S.T.Food Marketing Việt Nam",
    excerpt:
      "Sáng ngày 09/06/2021 Công ty NICON với cương vị là đơn vị Tổng thầu thiết kế - thi công đã tổ chức buổi Lễ Khởi Công Dự án Nhà máy S.T.FOOD MARKETING của Chủ đầu tư Thái Lan tại KCN VSIP II-A.",
    content: [
      "Sáng ngày 09/06/2021, NICON với vai trò Tổng thầu thiết kế – thi công đã long trọng tổ chức Lễ Khởi Công Dự án Nhà máy S.T.FOOD MARKETING tại KCN VSIP II-A, Bình Dương.",
      "Dự án có quy mô đầu tư lớn từ chủ đầu tư Thái Lan, đánh dấu sự tin tưởng của các tập đoàn quốc tế đối với năng lực Tổng thầu của NICON.",
      "Công trình áp dụng các tiêu chuẩn an toàn thực phẩm GMP và HACCP, với thời gian thi công dự kiến 14 tháng.",
    ],
    i18n: {
      en: {
        title: "Groundbreaking of S.T.Food Marketing Vietnam factory",
        excerpt:
          "On June 9, 2021, as Design–Build general contractor, NICON held the groundbreaking ceremony for the S.T.FOOD MARKETING factory of a Thai investor at VSIP II-A IP.",
        content: [
          "On the morning of June 9, 2021, NICON solemnly held the groundbreaking ceremony at VSIP II-A IP, Binh Duong as Design–Build general contractor.",
          "The large-scale project from a Thai investor signals international confidence in NICON's general contracting capabilities.",
          "The project applies GMP and HACCP food safety standards, with an estimated 14-month construction period.",
        ],
      },
      zh: {
        title: "S.T.Food Marketing 越南工厂项目奠基",
        excerpt:
          "2021年6月9日上午,NICON 作为设计施工总承包单位为泰国投资方在 VSIP II-A 工业园举行 S.T.FOOD MARKETING 工厂奠基仪式。",
        content: [
          "2021年6月9日上午,NICON 以设计施工总承包身份在平阳省 VSIP II-A 工业园隆重举行 S.T.FOOD MARKETING 工厂奠基仪式。",
          "该项目投资规模大,来自泰国投资方,体现了国际集团对 NICON 总承包能力的信任。",
          "工程应用 GMP 与 HACCP 食品安全标准,预计施工周期14个月。",
        ],
      },
      ja: {
        title: "S.T.Food Marketingベトナム工場プロジェクトの起工式",
        excerpt:
          "2021年6月9日、NICONは設計・施工一括の総合請負業者としてタイ投資家のS.T.FOOD MARKETING工場の起工式をVSIP II-A工業団地で執り行いました。",
        content: [
          "2021年6月9日朝、NICONは設計・施工一括の総合請負業者としてビンズン省VSIP II-A工業団地で起工式を厳粛に挙行しました。",
          "タイ投資家による大規模案件であり、国際企業からNICONの総合請負能力への信頼を示すものとなりました。",
          "GMPおよびHACCP食品安全基準を適用し、工期は14ヶ月を予定しています。",
        ],
      },
    },
  },
];

export const getActivityById = (id: string) => activities.find((a) => a.id === id);
