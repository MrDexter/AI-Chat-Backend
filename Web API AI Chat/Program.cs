using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;      // Talk to SQL Server
using System.Text;                   // Handle text + encoding
using OpenAI;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var OpenAIKey = builder.Configuration["OpenAI:API_Key"];

var openAiClient = new OpenAIClient(OpenAIKey);
var chatClient = new ChatClient("gpt-4o-mini", OpenAIKey);

var httpClient = new HttpClient();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/aichat", async () =>
{
    var result = new List<Message>();

    using (var connection = new SqlConnection(connectionString))
    {         
        await connection.OpenAsync();

        var sql = @"SELECT author, content, createdAt FROM Messages order by createdAt ASC";

        using (var command = new SqlCommand(sql, connection))
        using (var reader = await command.ExecuteReaderAsync())
        {

            while (await reader.ReadAsync())
            {
                result.Add(new Message(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetDateTime(2)
                ));
            };
        };
    };

    return Results.Ok(result);    
});

app.MapPost("/aichat", async (NewMessageDate newMessage) =>
{
    var result = new List<NewMessageDate>();
    await SavetoDB("user", newMessage.Content, connectionString);
    result.Add(new NewMessageDate(
        newMessage.Author,
        newMessage.Content,
        DateTime.UtcNow
    ));

    ChatCompletion completion = await chatClient.CompleteChatAsync(newMessage.Content);
    var response = completion.Content[0].Text ?? string.Empty;
    await SavetoDB("assistant", response, connectionString);
    result.Add(new NewMessageDate(
        "Assistant",
        response,
        DateTime.UtcNow
    ));

    return Results.Ok(result);

});

static async Task SavetoDB(string author,string content, string connectionString)
{
    // Message savedMessage;

    using (var connection = new SqlConnection(connectionString))
    {
      await connection.OpenAsync();

      var sql = @"INSERT INTO Messages (Author, Content) OUTPUT INSERTED.ID, INSERTED.Author, INSERTED.Content, INSERTED.CreatedAt VALUES (@Author, @Content);";
      
      using (var command = new SqlCommand(sql, connection))
      {
        command.Parameters.AddWithValue("@Author", author);
        command.Parameters.AddWithValue("@Content", content);

        using (var reader = await command.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
/*                 savedMessage = new Message(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDateTime(3)
                ); */
            }
            else 
            {
                Results.Problem("Message failed to insert");    
            };
        };
      };
    };
};

app.Run();

record Message(
    string Author,
    string Content,
    DateTime CreatedAt
);

record NewMessage(
    string Author,
    string Content
);

record NewMessageDate(
    string Author,
    string Content,
    DateTime CreatedAt
);

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
};


