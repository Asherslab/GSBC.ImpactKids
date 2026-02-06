using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.Authentication.UsersServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class UsersService(
    GsbcDbContext       db,
    IEventService<User> eventService,
    IConverter<DbUser, User> converter
) : IUsersService;