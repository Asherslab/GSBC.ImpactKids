using GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Elvanto;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Services.ElvantoServices;

public partial class ElvantoService
{
    [Authorize]
    public async Task<ElvantoServicePositionsResponse> GetServicePositions(CallContext context = default)
    {
        ServicesRequest request = new()
        {
            Start = DateOnly.FromDateTime(DateTime.Now),
            End = DateOnly.FromDateTime(DateTime.Now.AddMonths(3)),
            ServiceTypes = ["b4bead2d-2d49-4a39-8991-a81d97c10bf8"],
            Fields = ["volunteers"]
        };

        ServicesResponse? response = await SendMessage<ServicesRequest, ServicesResponse>(request);
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
            if (service.Volunteers == null)
                continue;

            services.Add(service.Date);

            foreach (Plan plan in service.Volunteers.Plan)
            {
                if (plan.Positions == null)
                    continue;

                plan.Positions.Position = plan.Positions.Position
                    .Where(x =>
                        x is { DepartmentName: "Children's Ministry", Volunteers: not null }
                    )
                    .ToList();

                foreach (Position position in plan.Positions.Position)
                {
                    ElvantoServicePosition? displayPosition =
                        positions.FirstOrDefault(x => x.Name == position.PositionName);

                    if (position.Volunteers!.Volunteer[0].Person == null)
                        continue;

                    Person person = position.Volunteers!.Volunteer[0].Person!;

                    if (displayPosition == null)
                    {
                        displayPosition = new ElvantoServicePosition
                        {
                            Name = position.PositionName!,
                            PositionsForService = new Dictionary<string, string>
                            {
                                { service.Date, $"{person.FirstName.Replace(" (Jnr)", "")}" }
                            }
                        };

                        positions.Add(displayPosition);
                        continue;
                    }

                    displayPosition.PositionsForService[service.Date] = $"{person.FirstName.Replace(" (Jnr)", "")}";
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