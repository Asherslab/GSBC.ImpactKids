namespace GSBC.ImpactKids.Shared.Contracts;

public static class ErrorConstants
{
    public const string PermissionDenied  = "Permission Denied";
    public const string ExceptionOccurred = "An Unexpected error occurred. Please try again later";

    public const string EventStreamIdNotFound =
        "Event service couldn't find stream id running. Might have disconnected?";

    public const string EventStreamNotRunning = "Event stream not running";

    public const string FailedToRetrieveServices = "Failed to retrieve elvanto services";

    public const string UserNotFound         = "User Not Found";
    public const string UserCannotToggleSelf = "User Cannot Enable Their Own User";
    public const string UserCannotDeleteSelf = "User Cannot Delete Their Own User";

    public const string PersonNotFound      = "Person Not Found";
    public const string PersonFirstNameNull = "Person First Name Must Be Set";
    public const string PersonLastNameNull  = "Person Last Name Must Be Set";

    public const string SchoolGradeNotFound             = "School Grade Not Found";
    public const string MediaConsentNotFound            = "Media Consent Value Not Found";
    public const string MedicalTypeNotFound             = "Medical Note Type Not Found";
    public const string AllergenNotFound                = "Allergen Not Found";
    public const string MedicalNoteNotFound             = "Medical Note Not Found";
    public const string AllergyNotFound                 = "Allergy Not Found";
    public const string MedicalNotesMustHaveTypeOrNotes = "Medical Notes Must Have A Type or Notes Text";
    public const string AllergiesMustHaveTypeOrNotes    = "Allergies Must Have A Type or Notes Text";

    public const string SchoolTermNotFound      = "School Term Not Found";
    public const string SchoolTermNameNull      = "School Term Name Must Be Set";
    public const string SchoolTermStartDateNull = "School Term Start Date Must Be Set";
    public const string SchoolTermEndDateNull   = "School Term End Date Must Be Set";

    public const string ServiceNotFound = "Service Not Found";
    public const string ServiceDateNull = "Service Date Must Be Set";

    public const string ServiceTypeNotFound  = "Service Type Not Found";
    public const string ServiceTypeLabelNull = "Service Type Label Must Be Set";

    public const string DollarStoreEntryNotFound = "Dollar Store Entry Not Found";
    public const string DollarStoreServiceNull   = "Dollar Store Must Have Service";
    public const string DollarStoreServiceExists = "Service can only have one Dollar Store Entry";

    public const string MemoryVerseListNotFound = "Memory Verse List Not Found";
    public const string MemoryVerseListNameNull = "Memory Verse List Name Must Be Set";

    public const string MemoryVerseNotFound           = "Memory Verse Not Found";
    public const string MemoryVerseReferenceNameNull  = "Memory Verse Reference Name Must Be Set";
    public const string MemoryVerseVerseNull          = "Memory Verse Text Must Be Set";
    public const string MemoryVerseServiceExists      = "Memory Verse Already Has Service Added";
    public const string MemoryVerseServiceNotFound    = "Memory Verse Does Not Have Service Added";
    public const string MemoryVerseBibleVerseExists   = "Memory Verse Already Has Bible Verse Added";
    public const string MemoryVerseBibleVerseNotFound = "Memory Verse Does Not Have Bible Verse Added";

    public const string BibleVerseNotFound = "Bible Verse Not Found";

    public const string MemorisationEntryNotFound = "Memorisation Entry Not Found";

    public const string AttendanceRecordNotFound             = "Attendance Record Not Found";
    public const string AttendanceRecordExists               = "This person has already been signed in during this service without being signed out, Please sign out person before creating another sign in";
    public const string AttendanceItemTypeNotFound           = "Attendance Item Type Not Found";
    public const string AttendanceItemRecordNotFound         = "Attendance Item Record Not Found";
    public const string AttendanceItemRecordReturnedRequired = "Attendance Item Record Item Requires Return Value";

    public const string GamePointRecordNotFound   = "Game Point Record Not Found";
    public const string GamePointRecordIdRequired = "Game Point Record Must Have A Client Generated Id";
    public const string GamePointRecordPointsZero = "Game Point Record Must Award A Non Zero Number Of Points";
    public const string GamePointRecordTeamIndex  = "Game Point Record Must Name A Team On The Board";
}