using GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Elvanto;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Elvanto;

namespace GSBC.ImpactKids.Grpc.Services.ElvantoServices;

public partial class ElvantoService
{
    private const string ImpactKidsDepartmentName = "Children's Ministry";
    private static readonly string[] ImpactKidsServiceTypes =
    [
        "b4bead2d-2d49-4a39-8991-a81d97c10bf8"
    ];

    private const string ProductionDepartmentName = "Production Ministry";
    private static readonly string[] ProductionServiceTypes =
    [
        "f891f318-bce8-11e0-9229-ea942707ad51",
        "0af6997e-d142-11e0-9229-ea942707ad51",
        "bb20e352-85d0-11e1-ab21-651537d68e43"
    ];

    public async Task<ElvantoServicePositionsResponse> GetServicePositions(
        ServicePositionsRequest request,
        CallContext             context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        string   departmentName;
        string[] serviceTypes;
        DateTime endDate;
        switch (request.Rosters)
        {
            default:
            case Rosters.ImpactKids:
                departmentName = ImpactKidsDepartmentName;
                serviceTypes = ImpactKidsServiceTypes;
                endDate = DateTime.Now.AddMonths(3);
                break;
            case Rosters.Production:
                departmentName = ProductionDepartmentName;
                serviceTypes = ProductionServiceTypes;
                endDate = DateTime.Now.AddMonths(1).AddDays(7);
                break;
        }

        DateTime startDate = request.StartDate ??= DateTime.Now;
        if (request.EndDate != null)
        {
            endDate = request.EndDate.Value;
        }

        ServicesRequest elvantoRequest = new()
        {
            Start = DateOnly.FromDateTime(startDate),
            End = DateOnly.FromDateTime(endDate.AddDays(1)),
            ServiceTypes = serviceTypes,
            Fields = ["volunteers"]
        };

        ServicesResponse? response = await SendMessage<ServicesRequest, ServicesResponse>(elvantoRequest, token);
        if (response?.Services == null)
        {
            return new ElvantoServicePositionsResponse
            {
                Success = false,
                Error = FailedToRetrieveServices
            };
        }

        List<ElvantoServicePosition> positions = [];
        List<string>                 services  = [];
        foreach (Service service in response.Services.Service)
        {
            if (service.Volunteers?.Plan == null)
                continue;

            services.Add(service.Date ?? "N/A");

            foreach (Plan plan in service.Volunteers.Plan)
            {
                if (plan.Positions == null)
                    continue;

                plan.Positions.Position = plan.Positions.Position
                    .Where(x =>
                        x.Volunteers != null &&
                        x.DepartmentName == departmentName
                    )
                    .ToList();

                foreach (Position position in plan.Positions.Position)
                {
                    ElvantoServicePosition? displayPosition =
                        positions.FirstOrDefault(x => x.Name == position.PositionName);

                    if (position.Volunteers!.Volunteer[0].Person == null)
                        continue;

                    ElvantoPerson person = position.Volunteers!.Volunteer[0].Person!;

                    if (displayPosition == null)
                    {
                        displayPosition = new ElvantoServicePosition
                        {
                            Name = position.PositionName!,
                            PositionsForService = new Dictionary<string, string>
                            {
                                { service.Date ?? "N/A", $"{person.FirstName?.Replace(" (Jnr)", "") ?? "N/A"}" }
                            }
                        };

                        positions.Add(displayPosition);
                        continue;
                    }

                    displayPosition.PositionsForService[service.Date ?? "N/A"] = $"{person.FirstName?.Replace(" (Jnr)", "") ?? "N/A"}";
                }
            }
        }

        return new ElvantoServicePositionsResponse
        {
            Success = true,

            Services = services,
            Positions = positions
        };
    }
}