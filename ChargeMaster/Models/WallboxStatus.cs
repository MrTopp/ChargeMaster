namespace ChargeMaster.Models;

public record WallboxStatus(
    string Serial,
    string Status,
    double CurrentLimit,
    double AccEnergy,
    int Phase1Current,
    int Phase2Current,
    int Phase3Current,
    DateTime? CurrentTime
);