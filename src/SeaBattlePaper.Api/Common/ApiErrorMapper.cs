using Microsoft.AspNetCore.Mvc;
using SeaBattlePaper.Application.Common;

namespace SeaBattlePaper.Api.Common;

public static class ApiErrorMapper
{
    public static IActionResult ToActionResult(this ControllerBase controller, OperationError error) =>
        error.Code switch
        {
            "match_not_found" => controller.NotFound(error),
            "unsupported_mode" or "invalid_fleet" or "invalid_coordinate" => controller.BadRequest(error),
            _ => controller.BadRequest(error)
        };
}
