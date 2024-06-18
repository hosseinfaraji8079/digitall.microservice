﻿using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Api.Filters;
using Asp.Versioning;
using Api.Controllers.Base;
using Api.Filters;
using Api.Utitlities;
using Application.Exceptions;
using Application.Extensions;
using Application.Helper;
using Application.Services.Interface.Account;
using Domain.DTOs.Account;
using Domain.Enums;
using Domain.Enums.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Api.Controllers.Account
{
    [ApiVersion(1)]
    [Route("api/v{v:apiVersion}/[controller]/[action]")]
    [ServiceFilter(typeof(ExceptionHandler))]
    [ApiResultFilter]
    [ApiController]
    public class UserController : ControllerBase
    {
        #region cosntructor

        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        #endregion

        #region login

        /// <summary>
        /// login user api after login active user phone
        /// </summary>
        /// <returns>Token</returns>
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ApiResult<LoginUserResponseDto>), (int)HttpStatusCode.OK)]
        [HttpPost]
        public async Task<ApiResult> Login([FromBody] LoginUserDto login)
        {
            LoginUserResult response = await _userService.LoginAsync(login);

            switch (response)
            {
                case LoginUserResult.NotFound:
                    return new ApiResult(false, ApiResultStatusCode.InValidUserPass, "کاربری با مشخصات زیر یافت نشد");
                case LoginUserResult.Blocked:
                    return new ApiResult(false, ApiResultStatusCode.Blocked, "حساب کاربری شما مسدود شدع است");
                case LoginUserResult.Success:
                    UserDto user = await _userService.GetUserByMobileAsync(login.Mobile!);
                    string token = JwtHelper.GenerateToken(user);
                    return new ApiResult<LoginUserResponseDto>
                    (true, ApiResultStatusCode.Success, new LoginUserResponseDto(token, user.UserFullName()),
                        $"{user.UserFullName()} خوش آمدید");
            }

            return NotFound();
        }

        #endregion

        #region register

        /// <summary>
        /// register user by phone number after register send password in sms
        /// </summary>
        /// <param name="register"></param>
        /// <returns></returns>
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NotFound)]
        [HttpPost]
        public async Task<ApiResult> Register([FromBody] RegisterUserDto register)
        {
            RegisterUserResult response = await _userService.RegisterAsync(register);

            switch (response)
            {
                case RegisterUserResult.Success:
                    return new ApiResult(true, ApiResultStatusCode.Success,
                        "ثبت نام با موفقیت انجام شد کلمه عبور از طریق پیامک ارسال شد");
                case RegisterUserResult.IsExists:
                    return new ApiResult(false, ApiResultStatusCode.Duplicate,
                        "با این شماره موبایل حساب کاربری وجود دارد");
            }

            return BadRequest();
        }

        #endregion

        #region forget-password

        /// <summary>
        /// when user forget password can get password by sms
        /// </summary>
        /// <remarks>any time send password after 2 min</remarks>\
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NotFound)]
        [HttpPost]
        public async Task<ApiResult> ForgetPassword([FromBody] ForgetUserPasswordDto forget)
        {
            ForgetPasswordResult response = await _userService.ForgetPasswordAsync(forget);

            switch (response)
            {
                case ForgetPasswordResult.Success:
                    return new ApiResult(true, ApiResultStatusCode.Success, "کلمه عبور جدید از طریق پیامک ارسال شد.");
                case ForgetPasswordResult.Posted:
                    return new ApiResult(false, ApiResultStatusCode.Blocked, "بعد از 3 دقیقه مجدد تلاش کنید");
                case ForgetPasswordResult.NotExists:
                    return NotFound();
                    break;
            }

            return BadRequest();
        }

        #endregion

        #region filter

        /// <summary>
        /// get all user by filter for show to admin
        /// </summary>
        /// <remarks>default value skip = 12 and take = 0</remarks>
        /// <param name="filter"></param>
        /// <permission cref="PermissionChecker"></permission>
        /// <returns>Lists Users</returns>
        [Authorize]
        [PermissionChecker("FilterUsers")]
        [ProducesResponseType(typeof(ApiResult<FilterUsersDto>), (int)HttpStatusCode.OK)]
        [HttpGet]
        public async Task<ApiResult<FilterUsersDto>> GetUsersByFilter([FromQuery] FilterUsersDto filter)
        {
            FilterUsersDto users = await _userService.GetUsersByFilterAsync(filter);

            return new OkObjectResult(users);
        }

        /// <summary>
        /// get users by filter for show to colleague by admin brand
        /// </summary>
        /// <remarks>default value skip = 12 and take = 0</remarks>
        /// <param name="filter"></param>
        /// <returns>Lists Users</returns>
        [Authorize]
        [HttpGet]
        [PermissionChecker("Users")]
        [ProducesResponseType(typeof(ApiResult<FilterUsersDto>), (int)HttpStatusCode.OK)]
        public async Task<ApiResult<FilterUsersDto>> GetAgentUsersFilter([FromQuery] FilterUsersDto filter)
        {
            FilterUsersDto users = await _userService.GetAgentUsersByFilterAsync(filter);

            return new OkObjectResult(users);
        }

        #endregion

        #region get user

        /// <summary>
        /// get user by Id
        /// </summary>
        /// <remarks>get user id form token</remarks>
        /// <returns>User</returns>
        [Authorize]
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<UserDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NoContent)]
        [ProducesDefaultResponseType]
        public async Task<ApiResult> GetUser()
        {
            UserDto user = await _userService.GetUserByIdAsync(User.GetUserId());

            return user switch
            {
                null => new NotFoundResult(),
                _ => new ApiResult<UserDto>(true, ApiResultStatusCode.Success, user, "")
            };
        }

        /// <summary>
        /// get user list by agent admin id form token این متد برای نمایش کاربران نمایندگی ها است 
        /// </summary>
        /// <remarks>get user list by agent admin id form token</remarks>
        /// <returns>User</returns>
        [Authorize]
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<List<UserDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NoContent)]
        [ProducesDefaultResponseType]
        public async Task<ApiResult<List<UserDto>>> GetAgentUsers()
        {
            List<UserDto> users = await _userService.GetUserByAgentAsync(User.GetUserId());

            return Ok(users);
        }

        #endregion

        #region update

        [HttpPut]
        [Authorize]
        [ProducesDefaultResponseType]
        public async Task<ApiResult> UpdateProfile([FromForm] UpdateUserProfileDto user)
        {
            UpdateUserProfileResult response = await _userService.UpdateUserProfileAsync(user, User.GetUserId());
            return Ok();
        }

        [HttpPut]
        public async Task<ApiResult> UpdateUser([FromBody] UpdateUserDto user)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NotFound)]
        [ProducesDefaultResponseType]
        public async Task<ApiResult> SendMobileActiveCode([FromQuery] string phone)
        {
            await _userService.SendMobileActiveCode(phone, User.GetUserId());
            return Ok();
        }

        #endregion

        #region add

        /// <summary>
        /// add user for admin
        /// </summary>
        /// <param name="user">user dto</param>
        /// <returns></returns>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NotFound)]
        [ProducesDefaultResponseType]
        public async Task<ApiResult> AddUser([FromForm] AddUserDto user)
        {
            AddUserResult response = await _userService.AddUserAsync(user, User.GetUserId());

            return response switch
            {
                AddUserResult.Success => Ok(),
                AddUserResult.IsExists => BadRequest("کاربر با این شماره تماس وجود دارد"),
                _ => NotFound()
            };
        }

        #endregion
    }
}