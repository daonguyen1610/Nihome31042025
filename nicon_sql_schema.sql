/*
  Converted from Nicon_Schema_Tables_Only.sql / Nicon_Database_Design.sql.
  Source schema is authoritative and already included exact SQL Server data types, defaults,
  check constraints, primary keys, and unique constraints.

  Structure adjustments in this file:
  - Flattened original multi-schema design into dbo tables
  - Renamed tables to snake_case with schema-aware names
  - Moved foreign keys inline into each CREATE TABLE block
  - Grouped tables by module, following the nihome_sql_schema.sql layout
*/

CREATE DATABASE [NiconDB];
GO
USE [NiconDB];
GO

-- =========================================================
-- App System
-- =========================================================
CREATE TABLE dbo.app_sys_users (
    Id           INT           NOT NULL IDENTITY(1,1),
    FullName     NVARCHAR(150) NOT NULL,
    Email        NVARCHAR(200) NOT NULL,
    PasswordHash VARCHAR(512)  NOT NULL,
    Phone        NVARCHAR(20)  NULL,
    AvatarUrl    NVARCHAR(500) NULL,
    LanguagePref VARCHAR(10)   NOT NULL CONSTRAINT DF_Users_LanguagePref DEFAULT('vi'),
    IsActive     BIT           NOT NULL CONSTRAINT DF_Users_IsActive     DEFAULT(1),
    CreatedAt    DATETIME2(7)  NOT NULL CONSTRAINT DF_Users_CreatedAt    DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2(7)  NOT NULL CONSTRAINT DF_Users_UpdatedAt    DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Users       PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_Users_Email UNIQUE NONCLUSTERED (Email ASC),
    CONSTRAINT CK_Users_Lang  CHECK (LanguagePref IN ('vi','en'))
);
GO

CREATE TABLE dbo.app_sys_roles (
    Id          INT           NOT NULL IDENTITY(1,1),
    Code        VARCHAR(50)   NOT NULL,
    Name        NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    CreatedAt   DATETIME2(7)  NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Roles      PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_Roles_Code UNIQUE NONCLUSTERED (Code ASC)
);
GO

CREATE TABLE dbo.app_sys_user_roles (
    UserId     INT          NOT NULL,
    RoleId     INT          NOT NULL,
    ProjectId  INT          NOT NULL CONSTRAINT DF_UserRoles_ProjectId  DEFAULT(0), -- 0 = system-wide
    AssignedAt DATETIME2(7) NOT NULL CONSTRAINT DF_UserRoles_AssignedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserId ASC, RoleId ASC, ProjectId ASC),
    CONSTRAINT FK_app_sys_user_roles_app_sys_users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_app_sys_user_roles_app_sys_roles_RoleId
        FOREIGN KEY (RoleId) REFERENCES dbo.app_sys_roles(Id)
);
GO

CREATE TABLE dbo.app_sys_permissions (
    Id       INT          NOT NULL IDENTITY(1,1),
    Module   VARCHAR(50)  NOT NULL,
    Resource VARCHAR(100) NOT NULL,
    Action   VARCHAR(50)  NOT NULL,
    Code     VARCHAR(200) NOT NULL,
    CONSTRAINT PK_Permissions      PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_Permissions_Code UNIQUE NONCLUSTERED (Code ASC)
);
GO

CREATE TABLE dbo.app_sys_role_permissions (
    RoleId       INT NOT NULL,
    PermissionId INT NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY CLUSTERED (RoleId ASC, PermissionId ASC),
    CONSTRAINT FK_app_sys_role_permissions_app_sys_roles_RoleId
        FOREIGN KEY (RoleId) REFERENCES dbo.app_sys_roles(Id),
    CONSTRAINT FK_app_sys_role_permissions_app_sys_permissions_PermissionId
        FOREIGN KEY (PermissionId) REFERENCES dbo.app_sys_permissions(Id)
);
GO

CREATE TABLE dbo.app_sys_audit_logs (
    Id        BIGINT        NOT NULL IDENTITY(1,1),
    UserId    INT           NOT NULL,
    Module    VARCHAR(50)   NOT NULL,
    Action    VARCHAR(50)   NOT NULL, -- CREATE|UPDATE|DELETE|APPROVE
    TableName VARCHAR(100)  NOT NULL,
    RecordId  INT           NOT NULL,
    OldValues NVARCHAR(MAX) NULL,  -- JSON
    NewValues NVARCHAR(MAX) NULL,  -- JSON
    IpAddress VARCHAR(45)   NULL,
    CreatedAt DATETIME2(7)  NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (Id ASC)
        WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON),
    CONSTRAINT FK_app_sys_audit_logs_app_sys_users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.app_sys_notifications (
    Id        BIGINT         NOT NULL IDENTITY(1,1),
    UserId    INT            NOT NULL,
    Module    VARCHAR(50)    NOT NULL,
    Title     NVARCHAR(200)  NOT NULL,
    Body      NVARCHAR(1000) NULL,
    LinkUrl   NVARCHAR(500)  NULL,
    IsRead    BIT            NOT NULL CONSTRAINT DF_Notifications_IsRead    DEFAULT(0),
    CreatedAt DATETIME2(7)   NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Notifications PRIMARY KEY CLUSTERED (Id ASC)
        WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON),
    CONSTRAINT FK_app_sys_notifications_app_sys_users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.app_sys_workflow_configs (
    Id             INT           NOT NULL IDENTITY(1,1),
    Module         VARCHAR(50)   NOT NULL,
    DocumentType   VARCHAR(100)  NOT NULL,
    StepOrder      INT           NOT NULL,
    StepName       NVARCHAR(100) NOT NULL,
    ApproverRoleId INT           NOT NULL,
    SkipCondition  NVARCHAR(500) NULL,
    IsActive       BIT           NOT NULL CONSTRAINT DF_WorkflowConfigs_IsActive DEFAULT(1),
    CONSTRAINT PK_WorkflowConfigs PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT FK_app_sys_workflow_configs_app_sys_roles_ApproverRoleId
        FOREIGN KEY (ApproverRoleId) REFERENCES dbo.app_sys_roles(Id)
);
GO

-- =========================================================
-- CRM
-- =========================================================
CREATE TABLE dbo.crm_customers (
    Id                  INT            NOT NULL IDENTITY(1,1),
    CustomerType        VARCHAR(20)    NOT NULL,
    FullName            NVARCHAR(200)  NOT NULL,
    CompanyName         NVARCHAR(200)  NULL,
    TaxCode             VARCHAR(20)    NULL,
    NationalId          VARCHAR(20)    NULL,
    Phone               NVARCHAR(20)   NULL,
    Email               NVARCHAR(200)  NULL,
    Address             NVARCHAR(500)  NULL,
    Province            NVARCHAR(100)  NULL,
    LegalRepresentative NVARCHAR(150)  NULL,
    LegalPosition       NVARCHAR(100)  NULL,
    Status              VARCHAR(30)    NOT NULL CONSTRAINT DF_Customers_Status    DEFAULT('Prospect'),
    Source              VARCHAR(50)    NULL,
    AssignedSaleId      INT            NULL,
    Notes               NVARCHAR(2000) NULL,
    DriveUrl            NVARCHAR(1000) NULL,
    CreatedBy           INT            NOT NULL,
    CreatedAt           DATETIME2(7)   NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(7)   NOT NULL CONSTRAINT DF_Customers_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Customers        PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_Customers_Type   CHECK (CustomerType IN ('Individual','Company')),
    CONSTRAINT CK_Customers_Status CHECK (Status IN ('Prospect','Active','Contracted','Inactive')),
    CONSTRAINT FK_crm_customers_app_sys_users_AssignedSaleId
        FOREIGN KEY (AssignedSaleId) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_crm_customers_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.crm_customer_contacts (
    Id         INT            NOT NULL IDENTITY(1,1),
    CustomerId INT            NOT NULL,
    FullName   NVARCHAR(150)  NOT NULL,
    Position   NVARCHAR(100)  NULL,
    Phone      NVARCHAR(20)   NULL,
    Email      NVARCHAR(200)  NULL,
    IsPrimary  BIT            NOT NULL CONSTRAINT DF_CustomerContacts_IsPrimary DEFAULT(0),
    Notes      NVARCHAR(500)  NULL,
    CONSTRAINT PK_CustomerContacts PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT FK_crm_customer_contacts_crm_customers_CustomerId
        FOREIGN KEY (CustomerId) REFERENCES dbo.crm_customers(Id)
);
GO

CREATE TABLE dbo.crm_customer_activities (
    Id           INT            NOT NULL IDENTITY(1,1),
    CustomerId   INT            NOT NULL,
    ActivityType VARCHAR(50)    NOT NULL,
    Subject      NVARCHAR(200)  NULL,
    Description  NVARCHAR(2000) NULL,
    ActivityDate DATETIME2(7)   NOT NULL,
    NextFollowUp DATETIME2(7)   NULL,
    UserId       INT            NOT NULL,
    CreatedAt    DATETIME2(7)   NOT NULL CONSTRAINT DF_CustomerActivities_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_CustomerActivities      PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_CustomerActivities_Type CHECK (ActivityType IN ('Call','Meeting','Email','Note')),
    CONSTRAINT FK_crm_customer_activities_crm_customers_CustomerId
        FOREIGN KEY (CustomerId) REFERENCES dbo.crm_customers(Id),
    CONSTRAINT FK_crm_customer_activities_app_sys_users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.crm_leads (
    Id                INT            NOT NULL IDENTITY(1,1),
    CustomerId        INT            NOT NULL,
    Title             NVARCHAR(200)  NOT NULL,
    Stage             VARCHAR(50)    NOT NULL CONSTRAINT DF_Leads_Stage    DEFAULT('New'),
    EstimatedValue    DECIMAL(18,2)  NULL,
    WinProbability    TINYINT        NULL,
    ExpectedCloseDate DATE           NULL,
    LeadSource        VARCHAR(50)    NULL,
    AssignedSaleId    INT            NULL,
    ClosedAt          DATETIME2(7)   NULL,
    ClosedReason      NVARCHAR(500)  NULL,
    Notes             NVARCHAR(2000) NULL,
    CreatedBy         INT            NOT NULL,
    CreatedAt         DATETIME2(7)   NOT NULL CONSTRAINT DF_Leads_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2(7)   NOT NULL CONSTRAINT DF_Leads_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Leads                PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_Leads_Stage          CHECK (Stage IN ('New','Qualified','Proposal','Negotiation','Won','Lost')),
    CONSTRAINT CK_Leads_WinProbability CHECK (WinProbability BETWEEN 0 AND 100),
    CONSTRAINT FK_crm_leads_crm_customers_CustomerId
        FOREIGN KEY (CustomerId) REFERENCES dbo.crm_customers(Id),
    CONSTRAINT FK_crm_leads_app_sys_users_AssignedSaleId
        FOREIGN KEY (AssignedSaleId) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_crm_leads_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.crm_quotations (
    Id                INT            NOT NULL IDENTITY(1,1),
    QuotationNo       VARCHAR(50)    NOT NULL,
    CustomerId        INT            NOT NULL,
    LeadId            INT            NULL,
    QuotationType     VARCHAR(30)    NOT NULL,
    ProjectScope      NVARCHAR(1000) NULL,
    SubTotal          DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Quotations_SubTotal    DEFAULT(0),
    DiscountPct       DECIMAL(5,2)   NOT NULL CONSTRAINT DF_Quotations_DiscountPct DEFAULT(0),
    VatPct            DECIMAL(5,2)   NOT NULL CONSTRAINT DF_Quotations_VatPct      DEFAULT(10),
    TotalAmount       DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Quotations_TotalAmount DEFAULT(0),
    Status            VARCHAR(30)    NOT NULL CONSTRAINT DF_Quotations_Status      DEFAULT('Draft'),
    Version           INT            NOT NULL CONSTRAINT DF_Quotations_Version     DEFAULT(1),
    ParentQuotationId INT            NULL,
    ValidUntil        DATE           NULL,
    Notes             NVARCHAR(2000) NULL,
    ApprovedBy        INT            NULL,
    ApprovedAt        DATETIME2(7)   NULL,
    CreatedBy         INT            NOT NULL,
    CreatedAt         DATETIME2(7)   NOT NULL CONSTRAINT DF_Quotations_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2(7)   NOT NULL CONSTRAINT DF_Quotations_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Quotations             PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_Quotations_QuotationNo UNIQUE NONCLUSTERED (QuotationNo ASC),
    CONSTRAINT CK_Quotations_Type        CHECK (QuotationType IN ('PerUnitRate','BOQ')),
    CONSTRAINT CK_Quotations_Status      CHECK (Status IN ('Draft','PendingApproval','Approved','Sent','Accepted','Rejected')),
    CONSTRAINT CK_Quotations_Discount    CHECK (DiscountPct BETWEEN 0 AND 100),
    CONSTRAINT CK_Quotations_Vat         CHECK (VatPct BETWEEN 0 AND 100),
    CONSTRAINT FK_crm_quotations_crm_customers_CustomerId
        FOREIGN KEY (CustomerId) REFERENCES dbo.crm_customers(Id),
    CONSTRAINT FK_crm_quotations_crm_leads_LeadId
        FOREIGN KEY (LeadId) REFERENCES dbo.crm_leads(Id),
    CONSTRAINT FK_crm_quotations_crm_quotations_ParentQuotationId
        FOREIGN KEY (ParentQuotationId) REFERENCES dbo.crm_quotations(Id),
    CONSTRAINT FK_crm_quotations_app_sys_users_ApprovedBy
        FOREIGN KEY (ApprovedBy) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_crm_quotations_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.crm_quotation_items (
    Id          INT            NOT NULL IDENTITY(1,1),
    QuotationId INT            NOT NULL,
    LineNumber  INT            NOT NULL,
    ItemCode    VARCHAR(50)    NULL,
    Description NVARCHAR(500)  NOT NULL,
    Unit        NVARCHAR(50)   NULL,
    Quantity    DECIMAL(18,4)  NULL,
    UnitPrice   DECIMAL(18,2)  NULL,
    TotalPrice  DECIMAL(18,2)  NULL,
    Notes       NVARCHAR(500)  NULL,
    CONSTRAINT PK_QuotationItems PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT FK_crm_quotation_items_crm_quotations_QuotationId
        FOREIGN KEY (QuotationId) REFERENCES dbo.crm_quotations(Id)
);
GO

CREATE TABLE dbo.crm_tenders (
    Id                 INT            NOT NULL IDENTITY(1,1),
    TenderNo           VARCHAR(50)    NOT NULL,
    CustomerId         INT            NOT NULL,
    LeadId             INT            NULL,
    TenderName         NVARCHAR(300)  NOT NULL,
    SubmissionDeadline DATETIME2(7)   NULL,
    EstimatedBudget    DECIMAL(18,2)  NULL,
    Status             VARCHAR(30)    NOT NULL CONSTRAINT DF_Tenders_Status    DEFAULT('Preparing'),
    ResultDate         DATE           NULL,
    WinAmount          DECIMAL(18,2)  NULL,
    Notes              NVARCHAR(2000) NULL,
    CreatedBy          INT            NOT NULL,
    CreatedAt          DATETIME2(7)   NOT NULL CONSTRAINT DF_Tenders_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt          DATETIME2(7)   NOT NULL CONSTRAINT DF_Tenders_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Tenders          PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_Tenders_TenderNo UNIQUE NONCLUSTERED (TenderNo ASC),
    CONSTRAINT CK_Tenders_Status   CHECK (Status IN ('Preparing','Submitted','Won','Lost','Cancelled')),
    CONSTRAINT FK_crm_tenders_crm_customers_CustomerId
        FOREIGN KEY (CustomerId) REFERENCES dbo.crm_customers(Id),
    CONSTRAINT FK_crm_tenders_crm_leads_LeadId
        FOREIGN KEY (LeadId) REFERENCES dbo.crm_leads(Id),
    CONSTRAINT FK_crm_tenders_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.crm_tender_documents (
    Id          INT            NOT NULL IDENTITY(1,1),
    TenderId    INT            NOT NULL,
    DocName     NVARCHAR(200)  NOT NULL,
    DocType     VARCHAR(50)    NULL,
    FileUrl     NVARCHAR(1000) NULL,
    DriveFileId VARCHAR(200)   NULL,
    Status      VARCHAR(30)    NOT NULL CONSTRAINT DF_TenderDocs_Status DEFAULT('NotReady'),
    DueDate     DATE           NULL,
    Notes       NVARCHAR(500)  NULL,
    UploadedBy  INT            NULL,
    UploadedAt  DATETIME2(7)   NULL,
    CONSTRAINT PK_TenderDocuments        PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_TenderDocuments_Status CHECK (Status IN ('NotReady','InProgress','Ready','Submitted')),
    CONSTRAINT FK_crm_tender_documents_crm_tenders_TenderId
        FOREIGN KEY (TenderId) REFERENCES dbo.crm_tenders(Id),
    CONSTRAINT FK_crm_tender_documents_app_sys_users_UploadedBy
        FOREIGN KEY (UploadedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.crm_surveys (
    Id               INT            NOT NULL IDENTITY(1,1),
    CustomerId       INT            NOT NULL,
    LeadId           INT            NULL,
    SurveyDate       DATE           NOT NULL,
    Location         NVARCHAR(500)  NULL,
    AssignedTo       INT            NOT NULL,
    WeatherCondition VARCHAR(50)    NULL,
    Summary          NVARCHAR(2000) NULL,
    DriveFolder      NVARCHAR(1000) NULL,
    Status           VARCHAR(30)    NOT NULL CONSTRAINT DF_Surveys_Status    DEFAULT('Planned'),
    CreatedBy        INT            NOT NULL,
    CreatedAt        DATETIME2(7)   NOT NULL CONSTRAINT DF_Surveys_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Surveys        PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_Surveys_Status CHECK (Status IN ('Planned','InProgress','Completed')),
    CONSTRAINT FK_crm_surveys_crm_customers_CustomerId
        FOREIGN KEY (CustomerId) REFERENCES dbo.crm_customers(Id),
    CONSTRAINT FK_crm_surveys_crm_leads_LeadId
        FOREIGN KEY (LeadId) REFERENCES dbo.crm_leads(Id),
    CONSTRAINT FK_crm_surveys_app_sys_users_AssignedTo
        FOREIGN KEY (AssignedTo) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_crm_surveys_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.crm_survey_attachments (
    Id          INT            NOT NULL IDENTITY(1,1),
    SurveyId    INT            NOT NULL,
    FileType    VARCHAR(20)    NOT NULL,
    FileName    NVARCHAR(300)  NOT NULL,
    FileUrl     NVARCHAR(1000) NULL,
    DriveFileId VARCHAR(200)   NULL,
    TakenAt     DATETIME2(7)   NULL,
    UploadedBy  INT            NULL,
    CONSTRAINT PK_SurveyAttachments          PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_SurveyAttachments_FileType CHECK (FileType IN ('Image','Video','File')),
    CONSTRAINT FK_crm_survey_attachments_crm_surveys_SurveyId
        FOREIGN KEY (SurveyId) REFERENCES dbo.crm_surveys(Id),
    CONSTRAINT FK_crm_survey_attachments_app_sys_users_UploadedBy
        FOREIGN KEY (UploadedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.crm_contracts (
    Id            INT            NOT NULL IDENTITY(1,1),
    ContractNo    VARCHAR(100)   NOT NULL,
    ContractName  NVARCHAR(300)  NOT NULL,
    CustomerId    INT            NOT NULL,
    QuotationId   INT            NULL,
    ContractValue DECIMAL(18,2)  NOT NULL,
    SignedDate    DATE           NULL,
    StartDate     DATE           NULL,
    EndDate       DATE           NULL,
    ContractType  VARCHAR(50)    NULL,
    Status        VARCHAR(30)    NOT NULL CONSTRAINT DF_Contracts_Status    DEFAULT('Draft'),
    FileUrl       NVARCHAR(1000) NULL,
    DriveFileId   VARCHAR(200)   NULL,
    Notes         NVARCHAR(2000) NULL,
    ApprovedBy    INT            NULL,
    ApprovedAt    DATETIME2(7)   NULL,
    CreatedBy     INT            NOT NULL,
    CreatedAt     DATETIME2(7)   NOT NULL CONSTRAINT DF_Contracts_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2(7)   NOT NULL CONSTRAINT DF_Contracts_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Contracts              PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_Contracts_ContractNo   UNIQUE NONCLUSTERED (ContractNo ASC),
    CONSTRAINT CK_Contracts_Type         CHECK (ContractType IN ('DesignOnly','DesignBuild','Fit-out') OR ContractType IS NULL),
    CONSTRAINT CK_Contracts_Status       CHECK (Status IN ('Draft','PendingApproval','Active','Suspended','Completed','Cancelled')),
    CONSTRAINT FK_crm_contracts_crm_customers_CustomerId
        FOREIGN KEY (CustomerId) REFERENCES dbo.crm_customers(Id),
    CONSTRAINT FK_crm_contracts_crm_quotations_QuotationId
        FOREIGN KEY (QuotationId) REFERENCES dbo.crm_quotations(Id),
    CONSTRAINT FK_crm_contracts_app_sys_users_ApprovedBy
        FOREIGN KEY (ApprovedBy) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_crm_contracts_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

-- =========================================================
-- Design
-- =========================================================
CREATE TABLE dbo.design_projects (
    Id              INT            NOT NULL IDENTITY(1,1),
    ProjectCode     VARCHAR(50)    NOT NULL,
    ProjectName     NVARCHAR(300)  NOT NULL,
    ContractId      INT            NULL,
    CustomerId      INT            NOT NULL,
    ProjectManager  INT            NOT NULL,
    Location        NVARCHAR(500)  NULL,
    ProjectType     VARCHAR(50)    NULL,
    Area            DECIMAL(10,2)  NULL,
    StoreyCount     INT            NULL,
    StartDate       DATE           NULL,
    ExpectedEndDate DATE           NULL,
    Status          VARCHAR(30)    NOT NULL CONSTRAINT DF_DesignProjects_Status    DEFAULT('Active'),
    DriveFolder     NVARCHAR(1000) NULL,
    Notes           NVARCHAR(2000) NULL,
    CreatedBy       INT            NOT NULL,
    CreatedAt       DATETIME2(7)   NOT NULL CONSTRAINT DF_DesignProjects_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(7)   NOT NULL CONSTRAINT DF_DesignProjects_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_DesignProjects             PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_DesignProjects_ProjectCode UNIQUE NONCLUSTERED (ProjectCode ASC),
    CONSTRAINT CK_DesignProjects_Status      CHECK (Status IN ('Active','OnHold','Completed','Cancelled')),
    CONSTRAINT CK_DesignProjects_Type        CHECK (ProjectType IN ('Residential','Commercial','Industrial','Renovation','Interior') OR ProjectType IS NULL),
    CONSTRAINT FK_design_projects_crm_contracts_ContractId
        FOREIGN KEY (ContractId) REFERENCES dbo.crm_contracts(Id),
    CONSTRAINT FK_design_projects_crm_customers_CustomerId
        FOREIGN KEY (CustomerId) REFERENCES dbo.crm_customers(Id),
    CONSTRAINT FK_design_projects_app_sys_users_ProjectManager
        FOREIGN KEY (ProjectManager) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_design_projects_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.design_phases (
    Id           INT            NOT NULL IDENTITY(1,1),
    ProjectId    INT            NOT NULL,
    PhaseType    VARCHAR(30)    NOT NULL,
    Title        NVARCHAR(200)  NOT NULL,
    Status       VARCHAR(30)    NOT NULL CONSTRAINT DF_DesignPhases_Status    DEFAULT('NotStarted'),
    StartDate    DATE           NULL,
    EndDate      DATE           NULL,
    LeadDesigner INT            NULL,
    Notes        NVARCHAR(1000) NULL,
    CreatedAt    DATETIME2(7)   NOT NULL CONSTRAINT DF_DesignPhases_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2(7)   NOT NULL CONSTRAINT DF_DesignPhases_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_DesignPhases           PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_DesignPhases_PhaseType CHECK (PhaseType IN ('Concept','BasicDesign','DetailedDesign')),
    CONSTRAINT CK_DesignPhases_Status    CHECK (Status IN ('NotStarted','InProgress','UnderReview','Approved','Completed')),
    CONSTRAINT FK_design_phases_design_projects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_design_phases_app_sys_users_LeadDesigner
        FOREIGN KEY (LeadDesigner) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.design_drawings (
    Id            INT            NOT NULL IDENTITY(1,1),
    PhaseId       INT            NOT NULL,
    DrawingNo     VARCHAR(100)   NOT NULL,
    DrawingName   NVARCHAR(300)  NOT NULL,
    Discipline    VARCHAR(50)    NULL,
    Revision      VARCHAR(20)    NOT NULL CONSTRAINT DF_Drawings_Revision  DEFAULT('R0'),
    Status        VARCHAR(30)    NOT NULL CONSTRAINT DF_Drawings_Status    DEFAULT('Draft'),
    IsIFC         BIT            NOT NULL CONSTRAINT DF_Drawings_IsIFC     DEFAULT(0),
    IFCDate       DATETIME2(7)   NULL,
    IFCApprovedBy INT            NULL,
    FileUrl       NVARCHAR(1000) NULL,
    DriveFileId   VARCHAR(200)   NULL,
    Scale         VARCHAR(20)    NULL,
    PaperSize     VARCHAR(10)    NULL,
    UploadedBy    INT            NOT NULL,
    UploadedAt    DATETIME2(7)   NOT NULL CONSTRAINT DF_Drawings_UploadedAt DEFAULT SYSUTCDATETIME(),
    CreatedAt     DATETIME2(7)   NOT NULL CONSTRAINT DF_Drawings_CreatedAt  DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Drawings             PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_Drawings_Discipline  CHECK (Discipline IN ('Architecture','Structure','MEP','Interior','Other') OR Discipline IS NULL),
    CONSTRAINT CK_Drawings_Status      CHECK (Status IN ('Draft','ForReview','Approved','IFC','Superseded','Voided')),
    CONSTRAINT FK_design_drawings_design_phases_PhaseId
        FOREIGN KEY (PhaseId) REFERENCES dbo.design_phases(Id),
    CONSTRAINT FK_design_drawings_app_sys_users_IFCApprovedBy
        FOREIGN KEY (IFCApprovedBy) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_design_drawings_app_sys_users_UploadedBy
        FOREIGN KEY (UploadedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.design_drawing_revisions (
    Id          INT            NOT NULL IDENTITY(1,1),
    DrawingId   INT            NOT NULL,
    Revision    VARCHAR(20)    NOT NULL,
    ChangeNote  NVARCHAR(1000) NULL,
    FileUrl     NVARCHAR(1000) NULL,
    DriveFileId VARCHAR(200)   NULL,
    UploadedBy  INT            NOT NULL,
    UploadedAt  DATETIME2(7)   NOT NULL CONSTRAINT DF_DrawingRevisions_UploadedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_DrawingRevisions PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT FK_design_drawing_revisions_design_drawings_DrawingId
        FOREIGN KEY (DrawingId) REFERENCES dbo.design_drawings(Id),
    CONSTRAINT FK_design_drawing_revisions_app_sys_users_UploadedBy
        FOREIGN KEY (UploadedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.design_feedbacks (
    Id           INT            NOT NULL IDENTITY(1,1),
    PhaseId      INT            NOT NULL,
    DrawingId    INT            NULL,
    FeedbackBy   INT            NOT NULL,
    FeedbackType VARCHAR(20)    NOT NULL,
    Decision     VARCHAR(30)    NOT NULL,
    Comment      NVARCHAR(2000) NULL,
    CreatedAt    DATETIME2(7)   NOT NULL CONSTRAINT DF_DesignFeedbacks_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_DesignFeedbacks              PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_DesignFeedbacks_FeedbackType CHECK (FeedbackType IN ('Internal','Client')),
    CONSTRAINT CK_DesignFeedbacks_Decision     CHECK (Decision IN ('Approved','Rejected','RevisionRequired')),
    CONSTRAINT FK_design_feedbacks_design_phases_PhaseId
        FOREIGN KEY (PhaseId) REFERENCES dbo.design_phases(Id),
    CONSTRAINT FK_design_feedbacks_design_drawings_DrawingId
        FOREIGN KEY (DrawingId) REFERENCES dbo.design_drawings(Id),
    CONSTRAINT FK_design_feedbacks_app_sys_users_FeedbackBy
        FOREIGN KEY (FeedbackBy) REFERENCES dbo.app_sys_users(Id)
);
GO

-- =========================================================
-- Permit
-- =========================================================
CREATE TABLE dbo.permit_items (
    Id               INT            NOT NULL IDENTITY(1,1),
    ProjectId        INT            NOT NULL,
    PermitType       VARCHAR(100)   NOT NULL,
    Title            NVARCHAR(300)  NOT NULL,
    IssuingAuthority NVARCHAR(200)  NULL,
    SubmissionDate   DATE           NULL,
    ExpectedDate     DATE           NULL,
    ActualIssueDate  DATE           NULL,
    ExpiryDate       DATE           NULL,
    Status           VARCHAR(30)    NOT NULL CONSTRAINT DF_PermitItems_Status    DEFAULT('NotStarted'),
    FileUrl          NVARCHAR(1000) NULL,
    DriveFileId      VARCHAR(200)   NULL,
    AssignedTo       INT            NULL,
    Notes            NVARCHAR(1000) NULL,
    CreatedBy        INT            NOT NULL,
    CreatedAt        DATETIME2(7)   NOT NULL CONSTRAINT DF_PermitItems_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2(7)   NOT NULL CONSTRAINT DF_PermitItems_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PermitItems        PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_PermitItems_Status CHECK (Status IN ('NotStarted','Preparing','Submitted','Approved','Rejected','Expired')),
    CONSTRAINT FK_permit_items_design_projects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_permit_items_app_sys_users_AssignedTo
        FOREIGN KEY (AssignedTo) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_permit_items_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.permit_activities (
    Id           INT            NOT NULL IDENTITY(1,1),
    PermitItemId INT            NOT NULL,
    ActivityDate DATETIME2(7)   NOT NULL,
    Description  NVARCHAR(1000) NOT NULL,
    UserId       INT            NOT NULL,
    FileUrl      NVARCHAR(1000) NULL,
    CreatedAt    DATETIME2(7)   NOT NULL CONSTRAINT DF_PermitActivities_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PermitActivities PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT FK_permit_activities_permit_items_PermitItemId
        FOREIGN KEY (PermitItemId) REFERENCES dbo.permit_items(Id),
    CONSTRAINT FK_permit_activities_app_sys_users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.app_sys_users(Id)
);
GO

-- =========================================================
-- Construction
-- =========================================================
CREATE TABLE dbo.construction_projects (
    Id              INT            NOT NULL IDENTITY(1,1),
    DesignProjectId INT            NOT NULL,
    SiteAddress     NVARCHAR(500)  NULL,
    SiteManager     INT            NOT NULL,
    ContractValue   DECIMAL(18,2)  NULL,
    StartDate       DATE           NULL,
    PlannedEndDate  DATE           NULL,
    ActualEndDate   DATE           NULL,
    Status          VARCHAR(30)    NOT NULL CONSTRAINT DF_ConProjects_Status    DEFAULT('PreConstruction'),
    DriveFolder     NVARCHAR(1000) NULL,
    CreatedBy       INT            NOT NULL,
    CreatedAt       DATETIME2(7)   NOT NULL CONSTRAINT DF_ConProjects_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(7)   NOT NULL CONSTRAINT DF_ConProjects_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ConProjects                 PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_ConProjects_DesignProjectId UNIQUE NONCLUSTERED (DesignProjectId ASC),
    CONSTRAINT CK_ConProjects_Status          CHECK (Status IN ('PreConstruction','UnderConstruction','CommissioningTest','Accepted','Completed')),
    CONSTRAINT FK_construction_projects_design_projects_DesignProjectId
        FOREIGN KEY (DesignProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_construction_projects_app_sys_users_SiteManager
        FOREIGN KEY (SiteManager) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_construction_projects_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.construction_gantt_tasks (
    Id                    INT            NOT NULL IDENTITY(1,1),
    ConstructionProjectId INT            NOT NULL,
    ParentTaskId          INT            NULL,
    TaskName              NVARCHAR(300)  NOT NULL,
    WBSCode               VARCHAR(50)    NULL,
    PlannedStart          DATE           NULL,
    PlannedEnd            DATE           NULL,
    ActualStart           DATE           NULL,
    ActualEnd             DATE           NULL,
    DurationDays          INT            NULL,
    ProgressPct           TINYINT        NOT NULL CONSTRAINT DF_GanttTasks_ProgressPct DEFAULT(0),
    AssignedTo            INT            NULL,
    Predecessors          VARCHAR(200)   NULL,
    IsMilestone           BIT            NOT NULL CONSTRAINT DF_GanttTasks_IsMilestone DEFAULT(0),
    Notes                 NVARCHAR(1000) NULL,
    CreatedAt             DATETIME2(7)   NOT NULL CONSTRAINT DF_GanttTasks_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt             DATETIME2(7)   NOT NULL CONSTRAINT DF_GanttTasks_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GanttTasks          PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_GanttTasks_Progress CHECK (ProgressPct BETWEEN 0 AND 100),
    CONSTRAINT FK_construction_gantt_tasks_construction_projects_ConstructionProjectId
        FOREIGN KEY (ConstructionProjectId) REFERENCES dbo.construction_projects(Id),
    CONSTRAINT FK_construction_gantt_tasks_construction_gantt_tasks_ParentTaskId
        FOREIGN KEY (ParentTaskId) REFERENCES dbo.construction_gantt_tasks(Id),
    CONSTRAINT FK_construction_gantt_tasks_app_sys_users_AssignedTo
        FOREIGN KEY (AssignedTo) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.construction_site_diary_entries (
    Id                    INT            NOT NULL IDENTITY(1,1),
    ConstructionProjectId INT            NOT NULL,
    EntryDate             DATE           NOT NULL,
    WeatherAM             NVARCHAR(50)   NULL,
    WeatherPM             NVARCHAR(50)   NULL,
    WorkerCount           INT            NULL,
    Summary               NVARCHAR(3000) NULL,
    SafetyIncidents       NVARCHAR(1000) NULL,
    MaterialsReceived     NVARCHAR(1000) NULL,
    Issues                NVARCHAR(1000) NULL,
    SubmittedBy           INT            NOT NULL,
    CreatedAt             DATETIME2(7)   NOT NULL CONSTRAINT DF_SiteDiaryEntries_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_SiteDiaryEntries            PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_SiteDiaryEntries_ProjDate   UNIQUE NONCLUSTERED (ConstructionProjectId ASC, EntryDate ASC),
    CONSTRAINT FK_construction_site_diary_entries_construction_projects_ConstructionProjectId
        FOREIGN KEY (ConstructionProjectId) REFERENCES dbo.construction_projects(Id),
    CONSTRAINT FK_construction_site_diary_entries_app_sys_users_SubmittedBy
        FOREIGN KEY (SubmittedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.construction_site_diary_attachments (
    Id          INT            NOT NULL IDENTITY(1,1),
    DiaryId     INT            NOT NULL,
    FileType    VARCHAR(20)    NOT NULL,
    FileName    NVARCHAR(300)  NULL,
    FileUrl     NVARCHAR(1000) NULL,
    DriveFileId VARCHAR(200)   NULL,
    TakenAt     DATETIME2(7)   NULL,
    UploadedBy  INT            NULL,
    CONSTRAINT PK_SiteDiaryAttachments          PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_SiteDiaryAttachments_FileType CHECK (FileType IN ('Image','Video','File')),
    CONSTRAINT FK_construction_site_diary_attachments_construction_site_diary_entries_DiaryId
        FOREIGN KEY (DiaryId) REFERENCES dbo.construction_site_diary_entries(Id),
    CONSTRAINT FK_construction_site_diary_attachments_app_sys_users_UploadedBy
        FOREIGN KEY (UploadedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.construction_inspection_requests (
    Id                    INT            NOT NULL IDENTITY(1,1),
    ConstructionProjectId INT            NOT NULL,
    GanttTaskId           INT            NULL,
    RequestNo             VARCHAR(50)    NOT NULL,
    WorkItem              NVARCHAR(300)  NOT NULL,
    InspectionType        VARCHAR(30)    NOT NULL,
    RequestedDate         DATE           NOT NULL,
    InspectionDate        DATE           NULL,
    Status                VARCHAR(30)    NOT NULL CONSTRAINT DF_InspectionReqs_Status    DEFAULT('Pending'),
    RequestedBy           INT            NOT NULL,
    InspectorId           INT            NULL,
    InspectionNote        NVARCHAR(2000) NULL,
    DriveFolder           NVARCHAR(1000) NULL,
    CreatedAt             DATETIME2(7)   NOT NULL CONSTRAINT DF_InspectionReqs_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt             DATETIME2(7)   NOT NULL CONSTRAINT DF_InspectionReqs_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_InspectionRequests              PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_InspectionRequests_RequestNo    UNIQUE NONCLUSTERED (RequestNo ASC),
    CONSTRAINT CK_InspectionRequests_Type         CHECK (InspectionType IN ('Partial','Final','Commissioning')),
    CONSTRAINT CK_InspectionRequests_Status       CHECK (Status IN ('Pending','Scheduled','Passed','Failed','ReInspection')),
    CONSTRAINT FK_construction_inspection_requests_construction_projects_ConstructionProjectId
        FOREIGN KEY (ConstructionProjectId) REFERENCES dbo.construction_projects(Id),
    CONSTRAINT FK_construction_inspection_requests_construction_gantt_tasks_GanttTaskId
        FOREIGN KEY (GanttTaskId) REFERENCES dbo.construction_gantt_tasks(Id),
    CONSTRAINT FK_construction_inspection_requests_app_sys_users_RequestedBy
        FOREIGN KEY (RequestedBy) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_construction_inspection_requests_app_sys_users_InspectorId
        FOREIGN KEY (InspectorId) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.construction_inspection_checklists (
    Id           INT            NOT NULL IDENTITY(1,1),
    InspectionId INT            NOT NULL,
    ItemNo       INT            NOT NULL,
    CheckItem    NVARCHAR(300)  NOT NULL,
    Standard     NVARCHAR(300)  NULL,
    Result       VARCHAR(20)    NULL,
    Remark       NVARCHAR(500)  NULL,
    PhotoUrl     NVARCHAR(1000) NULL,
    DriveFileId  VARCHAR(200)   NULL,
    CONSTRAINT PK_InspectionChecklists        PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_InspectionChecklists_Result CHECK (Result IN ('Pass','Fail','NA') OR Result IS NULL),
    CONSTRAINT FK_construction_inspection_checklists_construction_inspection_requests_InspectionId
        FOREIGN KEY (InspectionId) REFERENCES dbo.construction_inspection_requests(Id)
);
GO

CREATE TABLE dbo.construction_punchlist (
    Id                    INT            NOT NULL IDENTITY(1,1),
    ConstructionProjectId INT            NOT NULL,
    InspectionId          INT            NULL,
    IssueCode             VARCHAR(50)    NOT NULL,
    Description           NVARCHAR(1000) NOT NULL,
    Location              NVARCHAR(300)  NULL,
    Severity              VARCHAR(20)    NOT NULL CONSTRAINT DF_Punchlist_Severity DEFAULT('Medium'),
    Status                VARCHAR(20)    NOT NULL CONSTRAINT DF_Punchlist_Status   DEFAULT('Open'),
    AssignedTo            INT            NULL,
    DueDate               DATE           NULL,
    FixedDate             DATE           NULL,
    PhotoUrl              NVARCHAR(1000) NULL,
    ResolvedPhotoUrl      NVARCHAR(1000) NULL,
    DriveFileId           VARCHAR(200)   NULL,
    ReportedBy            INT            NOT NULL,
    CreatedAt             DATETIME2(7)   NOT NULL CONSTRAINT DF_Punchlist_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt             DATETIME2(7)   NOT NULL CONSTRAINT DF_Punchlist_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Punchlist           PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_Punchlist_IssueCode UNIQUE NONCLUSTERED (IssueCode ASC),
    CONSTRAINT CK_Punchlist_Severity  CHECK (Severity IN ('Low','Medium','High','Critical')),
    CONSTRAINT CK_Punchlist_Status    CHECK (Status IN ('Open','InProgress','Resolved','Verified','Closed')),
    CONSTRAINT FK_construction_punchlist_construction_projects_ConstructionProjectId
        FOREIGN KEY (ConstructionProjectId) REFERENCES dbo.construction_projects(Id),
    CONSTRAINT FK_construction_punchlist_construction_inspection_requests_InspectionId
        FOREIGN KEY (InspectionId) REFERENCES dbo.construction_inspection_requests(Id),
    CONSTRAINT FK_construction_punchlist_app_sys_users_AssignedTo
        FOREIGN KEY (AssignedTo) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_construction_punchlist_app_sys_users_ReportedBy
        FOREIGN KEY (ReportedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

-- =========================================================
-- Procurement
-- =========================================================
CREATE TABLE dbo.procurement_vendors (
    Id                INT            NOT NULL IDENTITY(1,1),
    VendorCode        VARCHAR(50)    NOT NULL,
    CompanyName       NVARCHAR(300)  NOT NULL,
    VendorType        VARCHAR(30)    NOT NULL,
    TaxCode           VARCHAR(20)    NULL,
    Phone             NVARCHAR(20)   NULL,
    Email             NVARCHAR(200)  NULL,
    Address           NVARCHAR(500)  NULL,
    ContactPerson     NVARCHAR(150)  NULL,
    LicenseNo         VARCHAR(100)   NULL,
    TradeCategory     NVARCHAR(300)  NULL,
    CapabilityFileUrl NVARCHAR(1000) NULL,
    DriveFolder       NVARCHAR(1000) NULL,
    IsActive          BIT            NOT NULL CONSTRAINT DF_Vendors_IsActive  DEFAULT(1),
    CreatedBy         INT            NOT NULL,
    CreatedAt         DATETIME2(7)   NOT NULL CONSTRAINT DF_Vendors_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2(7)   NOT NULL CONSTRAINT DF_Vendors_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Vendors            PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_Vendors_VendorCode UNIQUE NONCLUSTERED (VendorCode ASC),
    CONSTRAINT CK_Vendors_VendorType CHECK (VendorType IN ('Supplier','SubContractor','Both')),
    CONSTRAINT FK_procurement_vendors_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.procurement_vendor_evaluations (
    Id            INT            NOT NULL IDENTITY(1,1),
    VendorId      INT            NOT NULL,
    ProjectId     INT            NOT NULL,
    ScoreQuality  TINYINT        NULL,
    ScoreSchedule TINYINT        NULL,
    ScoreCost     TINYINT        NULL,
    ScoreSafety   TINYINT        NULL,
    EvaluatedBy   INT            NOT NULL,
    Comment       NVARCHAR(1000) NULL,
    EvaluatedAt   DATETIME2(7)   NOT NULL CONSTRAINT DF_VendorEval_EvaluatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_VendorEvaluations          PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_VendorEval_Quality  CHECK (ScoreQuality  BETWEEN 0 AND 10 OR ScoreQuality  IS NULL),
    CONSTRAINT CK_VendorEval_Schedule CHECK (ScoreSchedule BETWEEN 0 AND 10 OR ScoreSchedule IS NULL),
    CONSTRAINT CK_VendorEval_Cost     CHECK (ScoreCost     BETWEEN 0 AND 10 OR ScoreCost     IS NULL),
    CONSTRAINT CK_VendorEval_Safety   CHECK (ScoreSafety   BETWEEN 0 AND 10 OR ScoreSafety   IS NULL),
    CONSTRAINT FK_procurement_vendor_evaluations_procurement_vendors_VendorId
        FOREIGN KEY (VendorId) REFERENCES dbo.procurement_vendors(Id),
    CONSTRAINT FK_procurement_vendor_evaluations_design_projects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_procurement_vendor_evaluations_app_sys_users_EvaluatedBy
        FOREIGN KEY (EvaluatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.procurement_bid_comparisons (
    Id              INT            NOT NULL IDENTITY(1,1),
    ProjectId       INT            NOT NULL,
    WorkPackage     NVARCHAR(300)  NOT NULL,
    ItemDescription NVARCHAR(500)  NOT NULL,
    Unit            NVARCHAR(50)   NULL,
    Quantity        DECIMAL(18,4)  NULL,
    Notes           NVARCHAR(500)  NULL,
    CreatedBy       INT            NOT NULL,
    CreatedAt       DATETIME2(7)   NOT NULL CONSTRAINT DF_BidComparisons_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_BidComparisons PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT FK_procurement_bid_comparisons_design_projects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_procurement_bid_comparisons_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.procurement_bid_comparison_offers (
    Id              INT            NOT NULL IDENTITY(1,1),
    BidComparisonId INT            NOT NULL,
    VendorId        INT            NOT NULL,
    UnitPrice       DECIMAL(18,2)  NULL,
    TotalPrice      DECIMAL(18,2)  NULL,
    DeliveryDays    INT            NULL,
    Remarks         NVARCHAR(500)  NULL,
    IsSelected      BIT            NOT NULL CONSTRAINT DF_BidOffers_IsSelected DEFAULT(0),
    CONSTRAINT PK_BidComparisonOffers PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT FK_procurement_bid_comparison_offers_procurement_bid_comparisons_BidComparisonId
        FOREIGN KEY (BidComparisonId) REFERENCES dbo.procurement_bid_comparisons(Id),
    CONSTRAINT FK_procurement_bid_comparison_offers_procurement_vendors_VendorId
        FOREIGN KEY (VendorId) REFERENCES dbo.procurement_vendors(Id)
);
GO

CREATE TABLE dbo.procurement_materials (
    Id            INT            NOT NULL IDENTITY(1,1),
    MaterialCode  VARCHAR(50)    NOT NULL,
    MaterialName  NVARCHAR(300)  NOT NULL,
    Category      NVARCHAR(100)  NULL,
    Unit          NVARCHAR(30)   NOT NULL,
    StandardPrice DECIMAL(18,2)  NULL,
    Specification NVARCHAR(500)  NULL,
    IsActive      BIT            NOT NULL CONSTRAINT DF_Materials_IsActive  DEFAULT(1),
    CreatedAt     DATETIME2(7)   NOT NULL CONSTRAINT DF_Materials_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Materials             PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_Materials_MaterialCode UNIQUE NONCLUSTERED (MaterialCode ASC)
);
GO

CREATE TABLE dbo.procurement_project_boq (
    Id            INT            NOT NULL IDENTITY(1,1),
    ProjectId     INT            NOT NULL,
    MaterialId    INT            NOT NULL,
    PlannedQty    DECIMAL(18,4)  NOT NULL,
    EstimatedCost DECIMAL(18,2)  NULL,
    Notes         NVARCHAR(500)  NULL,
    CreatedBy     INT            NOT NULL,
    CreatedAt     DATETIME2(7)   NOT NULL CONSTRAINT DF_ProjectBOQ_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ProjectBOQ               PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_ProjectBOQ_ProjMaterial   UNIQUE NONCLUSTERED (ProjectId ASC, MaterialId ASC),
    CONSTRAINT FK_procurement_project_boq_design_projects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_procurement_project_boq_procurement_materials_MaterialId
        FOREIGN KEY (MaterialId) REFERENCES dbo.procurement_materials(Id),
    CONSTRAINT FK_procurement_project_boq_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.procurement_material_requests (
    Id            INT            NOT NULL IDENTITY(1,1),
    MRNo          VARCHAR(50)    NOT NULL,
    ProjectId     INT            NOT NULL,
    RequestedBy   INT            NOT NULL,
    RequestedDate DATE           NOT NULL,
    RequiredDate  DATE           NULL,
    Status        VARCHAR(30)    NOT NULL CONSTRAINT DF_MaterialRequests_Status    DEFAULT('Pending'),
    ApprovedBy    INT            NULL,
    ApprovedAt    DATETIME2(7)   NULL,
    Notes         NVARCHAR(1000) NULL,
    CreatedAt     DATETIME2(7)   NOT NULL CONSTRAINT DF_MaterialRequests_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_MaterialRequests        PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_MaterialRequests_MRNo   UNIQUE NONCLUSTERED (MRNo ASC),
    CONSTRAINT CK_MaterialRequests_Status CHECK (Status IN ('Pending','ApprovedPartial','Approved','Rejected','Issued')),
    CONSTRAINT FK_procurement_material_requests_design_projects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_procurement_material_requests_app_sys_users_RequestedBy
        FOREIGN KEY (RequestedBy) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_procurement_material_requests_app_sys_users_ApprovedBy
        FOREIGN KEY (ApprovedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.procurement_material_request_items (
    Id           INT            NOT NULL IDENTITY(1,1),
    MRId         INT            NOT NULL,
    MaterialId   INT            NOT NULL,
    RequestedQty DECIMAL(18,4)  NOT NULL,
    ApprovedQty  DECIMAL(18,4)  NULL,
    IssuedQty    DECIMAL(18,4)  NULL,
    Remarks      NVARCHAR(300)  NULL,
    CONSTRAINT PK_MaterialRequestItems PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT FK_procurement_material_request_items_procurement_material_requests_MRId
        FOREIGN KEY (MRId) REFERENCES dbo.procurement_material_requests(Id),
    CONSTRAINT FK_procurement_material_request_items_procurement_materials_MaterialId
        FOREIGN KEY (MaterialId) REFERENCES dbo.procurement_materials(Id)
);
GO

CREATE TABLE dbo.procurement_warehouse_transactions (
    Id           INT            NOT NULL IDENTITY(1,1),
    TxnNo        VARCHAR(50)    NOT NULL,
    ProjectId    INT            NOT NULL,
    TxnType      VARCHAR(10)    NOT NULL,
    MRItemId     INT            NULL,
    MaterialId   INT            NOT NULL,
    Quantity     DECIMAL(18,4)  NOT NULL,
    UnitPrice    DECIMAL(18,2)  NULL,
    TotalValue   DECIMAL(18,2)  NULL,
    IssuedToTeam NVARCHAR(100)  NULL,
    VendorId     INT            NULL,
    TxnDate      DATE           NOT NULL,
    Notes        NVARCHAR(500)  NULL,
    ProcessedBy  INT            NOT NULL,
    CreatedAt    DATETIME2(7)   NOT NULL CONSTRAINT DF_WarehouseTxns_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_WarehouseTransactions         PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_WarehouseTransactions_TxnNo   UNIQUE NONCLUSTERED (TxnNo ASC),
    CONSTRAINT CK_WarehouseTransactions_TxnType CHECK (TxnType IN ('IN','OUT')),
    CONSTRAINT FK_procurement_warehouse_transactions_design_projects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_procurement_warehouse_transactions_procurement_material_request_items_MRItemId
        FOREIGN KEY (MRItemId) REFERENCES dbo.procurement_material_request_items(Id),
    CONSTRAINT FK_procurement_warehouse_transactions_procurement_materials_MaterialId
        FOREIGN KEY (MaterialId) REFERENCES dbo.procurement_materials(Id),
    CONSTRAINT FK_procurement_warehouse_transactions_procurement_vendors_VendorId
        FOREIGN KEY (VendorId) REFERENCES dbo.procurement_vendors(Id),
    CONSTRAINT FK_procurement_warehouse_transactions_app_sys_users_ProcessedBy
        FOREIGN KEY (ProcessedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

-- =========================================================
-- Finance
-- =========================================================
CREATE TABLE dbo.finance_sub_contracts (
    Id            INT            NOT NULL IDENTITY(1,1),
    ContractNo    VARCHAR(100)   NOT NULL,
    ContractName  NVARCHAR(300)  NOT NULL,
    ProjectId     INT            NOT NULL,
    VendorId      INT            NOT NULL,
    WorkScope     NVARCHAR(1000) NULL,
    ContractValue DECIMAL(18,2)  NOT NULL,
    SignedDate    DATE           NULL,
    StartDate     DATE           NULL,
    EndDate       DATE           NULL,
    Status        VARCHAR(30)    NOT NULL CONSTRAINT DF_SubContracts_Status    DEFAULT('Draft'),
    FileUrl       NVARCHAR(1000) NULL,
    DriveFileId   VARCHAR(200)   NULL,
    Notes         NVARCHAR(1000) NULL,
    CreatedBy     INT            NOT NULL,
    CreatedAt     DATETIME2(7)   NOT NULL CONSTRAINT DF_SubContracts_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2(7)   NOT NULL CONSTRAINT DF_SubContracts_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_SubContracts            PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_SubContracts_ContractNo UNIQUE NONCLUSTERED (ContractNo ASC),
    CONSTRAINT CK_SubContracts_Status     CHECK (Status IN ('Draft','Active','Suspended','Completed','Terminated')),
    CONSTRAINT FK_finance_sub_contracts_design_projects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_finance_sub_contracts_procurement_vendors_VendorId
        FOREIGN KEY (VendorId) REFERENCES dbo.procurement_vendors(Id),
    CONSTRAINT FK_finance_sub_contracts_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.finance_contract_variation_orders (
    Id               INT            NOT NULL IDENTITY(1,1),
    VONo             VARCHAR(50)    NOT NULL,
    ClientContractId INT            NULL,
    SubContractId    INT            NULL,
    Description      NVARCHAR(1000) NOT NULL,
    ChangeType       VARCHAR(20)    NOT NULL,
    Amount           DECIMAL(18,2)  NOT NULL,
    Status           VARCHAR(30)    NOT NULL CONSTRAINT DF_ContractVOs_Status    DEFAULT('Pending'),
    ApprovedBy       INT            NULL,
    ApprovedAt       DATETIME2(7)   NULL,
    FileUrl          NVARCHAR(1000) NULL,
    Notes            NVARCHAR(1000) NULL,
    CreatedBy        INT            NOT NULL,
    CreatedAt        DATETIME2(7)   NOT NULL CONSTRAINT DF_ContractVOs_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ContractVOs            PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_ContractVOs_VONo       UNIQUE NONCLUSTERED (VONo ASC),
    CONSTRAINT CK_ContractVOs_ChangeType CHECK (ChangeType IN ('Increase','Decrease')),
    CONSTRAINT CK_ContractVOs_Status     CHECK (Status IN ('Pending','Approved','Rejected')),
    CONSTRAINT FK_finance_contract_variation_orders_crm_contracts_ClientContractId
        FOREIGN KEY (ClientContractId) REFERENCES dbo.crm_contracts(Id),
    CONSTRAINT FK_finance_contract_variation_orders_finance_sub_contracts_SubContractId
        FOREIGN KEY (SubContractId) REFERENCES dbo.finance_sub_contracts(Id),
    CONSTRAINT FK_finance_contract_variation_orders_app_sys_users_ApprovedBy
        FOREIGN KEY (ApprovedBy) REFERENCES dbo.app_sys_users(Id),
    CONSTRAINT FK_finance_contract_variation_orders_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.finance_payment_milestones (
    Id                 INT            NOT NULL IDENTITY(1,1),
    ClientContractId   INT            NOT NULL,
    MilestoneNo        INT            NOT NULL,
    Description        NVARCHAR(300)  NOT NULL,
    LinkedInspectionId INT            NULL,
    PlannedAmount      DECIMAL(18,2)  NOT NULL,
    PlannedDate        DATE           NULL,
    Status             VARCHAR(30)    NOT NULL CONSTRAINT DF_PaymentMilestones_Status DEFAULT('Pending'),
    CONSTRAINT PK_PaymentMilestones          PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_PaymentMilestones_ContrNum UNIQUE NONCLUSTERED (ClientContractId ASC, MilestoneNo ASC),
    CONSTRAINT CK_PaymentMilestones_Status   CHECK (Status IN ('Pending','Invoiced','PartialPaid','Paid')),
    CONSTRAINT FK_finance_payment_milestones_crm_contracts_ClientContractId
        FOREIGN KEY (ClientContractId) REFERENCES dbo.crm_contracts(Id),
    CONSTRAINT FK_finance_payment_milestones_construction_inspection_requests_LinkedInspectionId
        FOREIGN KEY (LinkedInspectionId) REFERENCES dbo.construction_inspection_requests(Id)
);
GO

CREATE TABLE dbo.finance_invoices (
    Id               INT            NOT NULL IDENTITY(1,1),
    InvoiceNo        VARCHAR(50)    NOT NULL,
    InvoiceType      VARCHAR(20)    NOT NULL,
    ClientContractId INT            NULL,
    SubContractId    INT            NULL,
    MilestoneId      INT            NULL,
    Amount           DECIMAL(18,2)  NOT NULL,
    VatPct           DECIMAL(5,2)   NULL,
    TotalAmount      DECIMAL(18,2)  NOT NULL,
    InvoiceDate      DATE           NOT NULL,
    DueDate          DATE           NULL,
    Status           VARCHAR(30)    NOT NULL CONSTRAINT DF_Invoices_Status     DEFAULT('Draft'),
    PaidAmount       DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Invoices_PaidAmount DEFAULT(0),
    Notes            NVARCHAR(1000) NULL,
    FileUrl          NVARCHAR(1000) NULL,
    CreatedBy        INT            NOT NULL,
    CreatedAt        DATETIME2(7)   NOT NULL CONSTRAINT DF_Invoices_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2(7)   NOT NULL CONSTRAINT DF_Invoices_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Invoices             PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_Invoices_InvoiceNo   UNIQUE NONCLUSTERED (InvoiceNo ASC),
    CONSTRAINT CK_Invoices_InvoiceType CHECK (InvoiceType IN ('Receivable','Payable')),
    CONSTRAINT CK_Invoices_Status      CHECK (Status IN ('Draft','Sent','PartialPaid','Paid','Overdue','Cancelled')),
    CONSTRAINT CK_Invoices_VatPct      CHECK (VatPct BETWEEN 0 AND 100 OR VatPct IS NULL),
    CONSTRAINT CK_Invoices_PaidAmount  CHECK (PaidAmount >= 0),
    CONSTRAINT FK_finance_invoices_crm_contracts_ClientContractId
        FOREIGN KEY (ClientContractId) REFERENCES dbo.crm_contracts(Id),
    CONSTRAINT FK_finance_invoices_finance_sub_contracts_SubContractId
        FOREIGN KEY (SubContractId) REFERENCES dbo.finance_sub_contracts(Id),
    CONSTRAINT FK_finance_invoices_finance_payment_milestones_MilestoneId
        FOREIGN KEY (MilestoneId) REFERENCES dbo.finance_payment_milestones(Id),
    CONSTRAINT FK_finance_invoices_app_sys_users_CreatedBy
        FOREIGN KEY (CreatedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.finance_payments (
    Id            INT            NOT NULL IDENTITY(1,1),
    InvoiceId     INT            NOT NULL,
    PaymentDate   DATE           NOT NULL,
    Amount        DECIMAL(18,2)  NOT NULL,
    PaymentMethod VARCHAR(30)    NULL,
    ReferenceNo   VARCHAR(100)   NULL,
    Note          NVARCHAR(500)  NULL,
    RecordedBy    INT            NOT NULL,
    CreatedAt     DATETIME2(7)   NOT NULL CONSTRAINT DF_Payments_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Payments               PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_Payments_Method        CHECK (PaymentMethod IN ('BankTransfer','Cash','Cheque') OR PaymentMethod IS NULL),
    CONSTRAINT CK_Payments_Amount        CHECK (Amount > 0),
    CONSTRAINT FK_finance_payments_finance_invoices_InvoiceId
        FOREIGN KEY (InvoiceId) REFERENCES dbo.finance_invoices(Id),
    CONSTRAINT FK_finance_payments_app_sys_users_RecordedBy
        FOREIGN KEY (RecordedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

CREATE TABLE dbo.finance_project_cost_entries (
    Id           INT            NOT NULL IDENTITY(1,1),
    ProjectId    INT            NOT NULL,
    CostCategory VARCHAR(100)   NOT NULL,
    Description  NVARCHAR(500)  NOT NULL,
    Amount       DECIMAL(18,2)  NOT NULL,
    EntryDate    DATE           NOT NULL,
    InvoiceId    INT            NULL,
    Notes        NVARCHAR(500)  NULL,
    RecordedBy   INT            NOT NULL,
    CreatedAt    DATETIME2(7)   NOT NULL CONSTRAINT DF_ProjectCostEntries_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ProjectCostEntries             PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_ProjectCostEntries_Category    CHECK (CostCategory IN ('Labor','Material','SubContract','Equipment','Overhead','Other')),
    CONSTRAINT CK_ProjectCostEntries_Amount      CHECK (Amount <> 0),
    CONSTRAINT FK_finance_project_cost_entries_design_projects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_finance_project_cost_entries_finance_invoices_InvoiceId
        FOREIGN KEY (InvoiceId) REFERENCES dbo.finance_invoices(Id),
    CONSTRAINT FK_finance_project_cost_entries_app_sys_users_RecordedBy
        FOREIGN KEY (RecordedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

-- =========================================================
-- DMS / Documents
-- =========================================================
CREATE TABLE dbo.dms_drive_folders (
    Id             INT            NOT NULL IDENTITY(1,1),
    ProjectId      INT            NOT NULL,
    ParentFolderId INT            NULL,
    FolderName     NVARCHAR(300)  NOT NULL,
    DriveId        VARCHAR(200)   NULL,
    DriveUrl       NVARCHAR(1000) NULL,
    FolderLevel    TINYINT        NOT NULL,
    FolderPath     NVARCHAR(2000) NULL,
    CreatedAt      DATETIME2(7)   NOT NULL CONSTRAINT DF_DriveFolders_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_DriveFolders             PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT CK_DriveFolders_FolderLevel CHECK (FolderLevel BETWEEN 1 AND 8),
    CONSTRAINT FK_dms_drive_folders_design_projects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_dms_drive_folders_dms_drive_folders_ParentFolderId
        FOREIGN KEY (ParentFolderId) REFERENCES dbo.dms_drive_folders(Id)
);
GO

CREATE TABLE dbo.dms_documents (
    Id           INT            NOT NULL IDENTITY(1,1),
    ProjectId    INT            NOT NULL,
    FolderId     INT            NULL,
    DocName      NVARCHAR(300)  NOT NULL,
    DocType      VARCHAR(50)    NULL,
    Module       VARCHAR(50)    NULL,
    RelatedTable VARCHAR(100)   NULL,
    RelatedId    INT            NULL,
    DriveFileId  VARCHAR(200)   NULL,
    DriveUrl     NVARCHAR(1000) NULL,
    LocalPath    NVARCHAR(1000) NULL,
    UploadedBy   INT            NOT NULL,
    UploadedAt   DATETIME2(7)   NOT NULL CONSTRAINT DF_Documents_UploadedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Documents PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT FK_dms_documents_design_projects_ProjectId
        FOREIGN KEY (ProjectId) REFERENCES dbo.design_projects(Id),
    CONSTRAINT FK_dms_documents_dms_drive_folders_FolderId
        FOREIGN KEY (FolderId) REFERENCES dbo.dms_drive_folders(Id),
    CONSTRAINT FK_dms_documents_app_sys_users_UploadedBy
        FOREIGN KEY (UploadedBy) REFERENCES dbo.app_sys_users(Id)
);
GO

-- =========================================================
-- Master Data
-- =========================================================
CREATE TABLE dbo.master_project_types (
    Id       INT            NOT NULL IDENTITY(1,1),
    Code     VARCHAR(50)    NOT NULL,
    NameVi   NVARCHAR(100)  NOT NULL,
    NameEn   NVARCHAR(100)  NULL,
    IsActive BIT            NOT NULL CONSTRAINT DF_ProjectTypes_IsActive DEFAULT(1),
    CONSTRAINT PK_ProjectTypes      PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_ProjectTypes_Code UNIQUE NONCLUSTERED (Code ASC)
);
GO

CREATE TABLE dbo.master_lead_sources (
    Id       INT            NOT NULL IDENTITY(1,1),
    Code     VARCHAR(50)    NOT NULL,
    NameVi   NVARCHAR(100)  NOT NULL,
    NameEn   NVARCHAR(100)  NULL,
    IsActive BIT            NOT NULL CONSTRAINT DF_LeadSources_IsActive DEFAULT(1),
    CONSTRAINT PK_LeadSources      PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_LeadSources_Code UNIQUE NONCLUSTERED (Code ASC)
);
GO

CREATE TABLE dbo.master_provinces (
    Id     INT           NOT NULL IDENTITY(1,1),
    Code   VARCHAR(10)   NOT NULL,
    NameVi NVARCHAR(100) NOT NULL,
    Region VARCHAR(50)   NULL,
    CONSTRAINT PK_Provinces      PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_Provinces_Code UNIQUE NONCLUSTERED (Code ASC)
);
GO

CREATE TABLE dbo.master_checklist_templates (
    Id           INT            NOT NULL IDENTITY(1,1),
    TemplateCode VARCHAR(50)    NOT NULL,
    Name         NVARCHAR(200)  NOT NULL,
    Module       VARCHAR(50)    NOT NULL,
    WorkItem     NVARCHAR(200)  NULL,
    IsActive     BIT            NOT NULL CONSTRAINT DF_ChecklistTemplates_IsActive  DEFAULT(1),
    CreatedAt    DATETIME2(7)   NOT NULL CONSTRAINT DF_ChecklistTemplates_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ChecklistTemplates        PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT UQ_ChecklistTemplates_Code   UNIQUE NONCLUSTERED (TemplateCode ASC),
    CONSTRAINT CK_ChecklistTemplates_Module CHECK (Module IN ('Construction','Permit'))
);
GO

CREATE TABLE dbo.master_checklist_template_items (
    Id         INT            NOT NULL IDENTITY(1,1),
    TemplateId INT            NOT NULL,
    ItemNo     INT            NOT NULL,
    CheckItem  NVARCHAR(300)  NOT NULL,
    Standard   NVARCHAR(300)  NULL,
    IsRequired BIT            NOT NULL CONSTRAINT DF_ChecklistTemplateItems_IsRequired DEFAULT(1),
    CONSTRAINT PK_ChecklistTemplateItems PRIMARY KEY CLUSTERED (Id ASC),
    CONSTRAINT FK_master_checklist_template_items_master_checklist_templates_TemplateId
        FOREIGN KEY (TemplateId) REFERENCES dbo.master_checklist_templates(Id)
);
GO
