﻿namespace WebAPI.DTO
{
	public class AuthLoginResponse(string token)
	{
		public string Token { get; set; } = token;
	}
}
