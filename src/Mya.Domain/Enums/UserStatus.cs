namespace Mya.Domain.Enums;

/// <summary>
/// PendingApproval becomes Active (approved) or Declined; Active can toggle to and from Suspended.
/// Declined and Suspended users still exist so login can tell them why they are refused.
/// </summary>
public enum UserStatus
{
    PendingApproval = 0,
    Active = 1,
    Suspended = 2,
    Declined = 3,
}
