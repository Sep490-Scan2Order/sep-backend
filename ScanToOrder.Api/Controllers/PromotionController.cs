using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Promotion;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Api.Controllers;

[Authorize(Roles = "Tenant")]
public class PromotionController : BaseController
{
    private readonly IPromotionService _promotionService;
    private readonly IAuthenticatedUserService _authenticatedUserService;

    public PromotionController(IPromotionService promotionService, IAuthenticatedUserService authenticatedUserService)
    {
        _promotionService = promotionService;
        _authenticatedUserService = authenticatedUserService;
    }
    
    [HttpPost]
    public async Task<ActionResult<ApiResponse<string>>> CreatePromotion([FromBody] CreatePromotionDto request)
    {
        if (_authenticatedUserService.ProfileId == null) throw new DomainException(AuthMessage.AuthError.USER_PROFILE_NOT_FOUND);
        var tenantId = _authenticatedUserService.ProfileId.Value;
        await _promotionService.CreatePromotionAsync(tenantId, request);
        return Success(string.Empty); 
    }
    
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PromotionResponseDto>>> GetPromotionById([FromRoute] int id)
    {
        var result = await _promotionService.GetPromotionByIdAsync(id);
        return Success(result);
    }
    
    [HttpPut]
    public async Task<ActionResult<ApiResponse<string>>> UpdatePromotion([FromBody] UpdatePromotionDto request)
    {
        await _promotionService.UpdatePromotionAsync(request);
        return Success(string.Empty); 
    }
    
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<string>>> DeletePromotion([FromRoute] int id)
    {
        await _promotionService.DeletePromotionAsync(id);
        return Success(string.Empty); 
    }
}