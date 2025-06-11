using Microsoft.AspNetCore.Mvc;
using Renci.SshNet;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WebAPI.Controllers
{
	[ApiController]
    [Route("api/[controller]")]
    public class ComputerController(ILogger<ComputerController> logger, IConfiguration configuration) : ControllerBase
    {
		private readonly ILogger<ComputerController> _logger = logger;
		private readonly IConfiguration _configuration = configuration;

		private readonly string _customRDPRuleName = "Remote Desktop - Custom Rule";

		[HttpPost("wake")]
		public IActionResult Wake()
		{
			_logger.LogInformation("Wake endpoint was called");

			try
			{
				byte[] macBytes = PhysicalAddress.Parse(_configuration.GetValue<string>("MACAddress").Replace(":", "")).GetAddressBytes();
				byte[] packet = new byte[102];
				for (int i = 0; i < 6; i++) packet[i] = 0xFF;
				for (int i = 1; i <= 16; i++)
				{
					Buffer.BlockCopy(macBytes, 0, packet, i * 6, 6);
				}

				using UdpClient client = new();
				client.Connect(_configuration.GetValue<string>("IPAddress"), 9);
				client.Send(packet);

				_logger.LogInformation("Wake-on-LAN packet sent");
				return Ok();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to send WakeOnLAN packet");
			}

			return StatusCode(500, "An error occurred while sending the Wake-on-LAN packet");
		}

		[HttpPost("enable-rdp")]
		public IActionResult EnableRDP()
		{
			try
			{
				using SshClient sshClient = new(_configuration["IPAddress"], _configuration["Auth:Username"], _configuration["Auth:Password"]);
				sshClient.Connect();
				if (sshClient.IsConnected)
				{
					string powershellCommand = "(Get-NetFirewallRule | Where-Object { $_.DisplayName -like '" + _customRDPRuleName + "' }).Count";
					string sshCommand = $"pwsh -NoProfile -ExecutionPolicy Bypass -Command \"{powershellCommand}\"";
					SshCommand cmd = sshClient.CreateCommand(sshCommand);
					string result = cmd.Execute().Trim();
					string error = cmd.Error;

					if (!string.IsNullOrEmpty(error))
					{
						_logger.LogInformation("SSH Command output: {result}", result);
						_logger.LogError("SSH Command error: {error}", error);
						throw new Exception("Failed to retrieve existing firewall rule");
					}

					if (result == "0")
					{
						List<string> ips = _configuration.GetSection("AllowedRDPIPAddresses").Get<List<string>>();
						powershellCommand = $"New-NetFirewallRule -DisplayName '{_customRDPRuleName}' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 3389 -RemoteAddress {string.Join(",", ips)} -Profile Any";
						sshCommand = $"pwsh -NoProfile -ExecutionPolicy Bypass -Command \"{powershellCommand}\"";
						cmd = sshClient.CreateCommand(sshCommand);
						result = cmd.Execute();
						error = cmd.Error;

						if (!string.IsNullOrEmpty(error))
						{
							_logger.LogInformation("SSH Command output: {result}", result);
							_logger.LogError("SSH Command error: {error}", error);
							throw new Exception("Failed to create firewall rule");
						}
					}

					powershellCommand = "$action = New-ScheduledTaskAction -Execute 'pwsh' -Argument '-Command Remove-NetFirewallRule -DisplayName ''" + _customRDPRuleName + "'' '; " +
						"$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddHours(2); " +
						"Register-ScheduledTask -TaskName 'DisableRDPCustomRule' -Action $action -Trigger $trigger -User 'SYSTEM' -RunLevel Highest -Force";
					sshCommand = $"pwsh -NoProfile -ExecutionPolicy Bypass -Command \"{powershellCommand}\"";
					cmd = sshClient.CreateCommand(sshCommand);
					result = cmd.Execute();
					error = cmd.Error;

					if (!string.IsNullOrEmpty(error))
					{
						_logger.LogInformation("SSH Command output: {result}", result);
						_logger.LogError("SSH Command error: {error}", error);
						throw new Exception("Failed to create schedule task");
					}
				}
				else
				{
					_logger.LogError("SSH connection failed");
					return StatusCode(500, "SSH connection failed");
				}
				sshClient.Disconnect();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Enabling RDP failed");
				return StatusCode(500, "Enabling RDP failed");
			}
			return Ok();
		}
	}
}
