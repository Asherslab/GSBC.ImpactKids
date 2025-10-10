namespace GSBC.ImpactKids.Shared.Contracts;

public static class ErrorConstants
{
    public const string PermissionDenied  = "Permission Denied";
    public const string ExceptionOccurred = "An Unexpected error occurred. Please try again later";

    public const string FailedToRetrieveServices = "Failed to retrieve elvanto services";

    public const string SchoolTermNotFound = "School Term Not Found";
    public const string SchoolTermNameNull = "School Term Name Must Be Set";
    public const string SchoolTermStartDateNull = "School Term Start Date Must Be Set";
    public const string SchoolTermEndDateNull = "School Term End Date Must Be Set";

    public const string ServiceNotFound = "Service Not Found";
    public const string ServiceSchoolTermNull = "Service School Term Must Be Set";
    public const string ServiceDateNull = "Service Date Must Be Set";

}