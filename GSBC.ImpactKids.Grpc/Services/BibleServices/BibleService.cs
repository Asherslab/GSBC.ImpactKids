using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Services.BibleServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class BibleService : IBibleService;