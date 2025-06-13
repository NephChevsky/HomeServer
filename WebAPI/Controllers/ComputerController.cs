using Microsoft.AspNetCore.Mvc;
using Renci.SshNet;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using WebAPI.DTO;

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
		public async Task<IActionResult> EnableRDPAsync()
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

				await ToggleLiveBoxRule(true);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Enabling RDP failed");
				return StatusCode(500, "Enabling RDP failed");
			}
			return Ok();
		}

		private async Task ToggleLiveBoxRule(bool enabled)
		{
			HttpClient client = new()
			{
				BaseAddress = new Uri($"http://{_configuration["Livebox:Url"]}/ws")
			};
			HttpRequestMessage request = new(HttpMethod.Post, "")
			{
				Content = JsonContent.Create(new LiveboxRequest("sah.Device.Information", "createContext", new() {
					{ "applicationName", "webui" },
					{ "username", _configuration["Livebox:Username"] },
					{ "password", _configuration["Livebox:Password"] }
				}))
			};
			request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-sah-ws-4-call+json");
			request.Headers.Add("Authorization", "X-Sah-Login");
			HttpResponseMessage response = await client.SendAsync(request);
			string contextId = (await response.Content.ReadFromJsonAsync<LiveboxResponse>()).Data["contextID"].ToString();

			if (enabled)
			{
				request = new(HttpMethod.Post, "")
				{
					Content = JsonContent.Create(new LiveboxRequest("Firewall", "setPortForwarding", new()
					{
						{ "id", "RDP" },
						{ "internalPort", "3389" },
						{ "externalPort", _configuration["RdpPort"] },
						{ "destinationIPAddress", _configuration["IPAddress"] },
						{ "enable", true },
						{ "persistent", true },
						{ "protocol", "6" },
						{ "description", "RDP" },
						{ "sourceInterface", "data" },
						{ "origin", "webui" },
						{ "destinationMACAddress", "" }
					}))
				};
			}
			else
			{
				request = new(HttpMethod.Post, "")
				{
					Content = JsonContent.Create(new LiveboxRequest("Firewall", "deletePortForwarding", new()
					{
						{ "id", "webui_RDP" },
						{ "destinationIPAddress", _configuration["IPAddress"] },
						{ "origin", "webui" }
					}))
				};
			}

			request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-sah-ws-4-call+json");
			request.Headers.Add("Authorization", $"X-Sah {contextId}");
			request.Headers.Add("X-Content", contextId);

			response = await client.SendAsync(request);
			await response.Content.ReadFromJsonAsync<LiveboxResponse>();
		}
	}
}
