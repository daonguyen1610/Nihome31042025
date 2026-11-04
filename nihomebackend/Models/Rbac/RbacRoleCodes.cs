namespace NihomeBackend.Models.Rbac;

/// <summary>
/// Canonical codes for built-in roles seeded by the application. Role.Code
/// remains a string because administrators may add custom roles at runtime.
/// </summary>
public static class RbacRoleCodes
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string Admin = "ADMIN";
    public const string User = "USER";
    public const string Sale = "SALE";
    public const string SalesManager = "SALES_MANAGER";
    public const string Design = "DESIGN";
    public const string DesignManager = "DESIGN_MANAGER";
    public const string DesignLead = "DESIGN_LEAD";
    public const string Architect = "ARCHITECT";
    public const string MepEngineer = "MEP_ENGINEER";
    public const string StructuralEngineer = "STRUCT_ENGINEER";
    public const string ProjectManager = "PM";
    public const string SiteCommander = "SITE_COMMANDER";
    public const string SiteQaQc = "SITE_QAQC";
    public const string LegalOfficer = "LEGAL_OFFICER";
    public const string QuantitySurveyor = "QS";
    public const string Procurement = "PROCUREMENT";
    public const string ProcurementManager = "PROCUREMENT_MANAGER";
    public const string Accountant = "ACCOUNTANT";
    public const string HumanResources = "HR";
    public const string Warehouse = "WAREHOUSE";
    public const string BoardOfDirectors = "BGD";
}

public static class RoleGroupCodes
{
    public const string Construction = "CONSTRUCTION";
    public const string Crm = "CRM";
    public const string Design = RbacRoleCodes.Design;
    public const string Finance = "FINANCE";
    public const string HumanResources = RbacRoleCodes.HumanResources;
    public const string Procurement = RbacRoleCodes.Procurement;
}
