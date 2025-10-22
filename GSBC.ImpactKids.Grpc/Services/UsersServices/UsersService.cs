using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Services.UsersServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class UsersService(
    GsbcDbContext       db,
    IEventService<User> eventService,
    IConverter<DbUser, User> converter
) : IUsersService;