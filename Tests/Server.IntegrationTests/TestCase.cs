using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Server.Contracts;
using Server.Data;

namespace Server.IntegrationTests;

public static class TestCase
{
    public static HttpClient CreateTestHttpClient(WebApplicationFactory<Program> factory, string dbname)
    {
        return factory
            .WithWebHostBuilder(builder => 
            {
                builder.ConfigureServices(services =>
                {
                    services.Remove(services.SingleOrDefault(service =>
                        service.ServiceType == typeof(DbContextOptions<AppDbContext>))!);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase(dbname));
                });
            })
            .CreateClient();
    }
    
    public static async Task<LoginResponse> Login(HttpClient client, string email, string password)
    {
        var model = new LoginRequest(email, password);
        var response = await client.PostAsJsonAsync("api/v1/auth/login", model);

        var loginResponse = await DeserializeResponse<LoginResponse>(response);
        
        IncludeTokenInRequest(client, loginResponse.AccessToken);
        
        return loginResponse;
    }
    
    public static void IncludeTokenInRequest(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
    
    public static async Task<T> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var jsonResponse = await response.Content.ReadAsStringAsync(); 
        return JsonConvert.DeserializeObject<T>(jsonResponse)!;
    }
}