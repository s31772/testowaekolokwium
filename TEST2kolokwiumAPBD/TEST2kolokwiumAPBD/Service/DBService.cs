using Microsoft.Data.SqlClient;
using TEST2kolokwiumAPBD.DTOs;
using TEST2kolokwiumAPBD.Exceptions;
using TEST2kolokwiumAPBD.Models;


namespace TEST2kolokwiumAPBD;

public interface IDBService
{
    Task<IEnumerable<StudentWithGroupDto>> GetAllStudentsAsync(string? firstname);
    Task<StudentWithGroupDto>CreateStudentAsync(StudentCreateDto studentData);
}
public class DBService(IConfiguration configuration) : IDBService
{
    public async Task<IEnumerable<StudentWithGroupDto>> GetAllStudentsAsync(string? firstname)
    {
        var result = new Dictionary<int,StudentWithGroupDto>();
        var connectionString = configuration.GetConnectionString("Default");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var commandSql = "SELECT s.Id, s.FirstName, s.LastName, s.Age, g.Id, g.Name FROM Student s " + 
                         "INNER JOIN GroupAssignment ON s.Id = GroupAssignment.Student_Id " + 
                         "INNER JOIN [Group] g ON g.Id = GroupAssignment.Group_Id ";

        if (!string.IsNullOrEmpty(firstname))
        {
            commandSql += "WHERE s.FirstName like '%' + @firstname + '%'";
        }
        await using var command = new SqlCommand(commandSql, connection);

        if (!string.IsNullOrEmpty(firstname))
        {
            command.Parameters.AddWithValue("@firstname", firstname);
        }

        
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var studentId = reader.GetInt32(0);
            
            if (!result.TryGetValue(studentId, out var studentWithGroupDto)) 
            {
                 studentWithGroupDto = new StudentWithGroupDto
                {
                    Id = studentId,
                    FirstName = reader.GetString(1),
                    LastName = reader.GetString(2),
                    Age = reader.GetInt32(3),
                    Group = []
                };
                
                result.Add(studentId, studentWithGroupDto);
            }

            if (!await reader.IsDBNullAsync(4))
            {
                studentWithGroupDto.Group.Add(new GroupGETDto
                {
                    Id = reader.GetInt32(4),
                    Name = reader.GetString(5),
                });
            }
        }
        
        return result.Values;
    }

    public async Task<StudentWithGroupDto> CreateStudentAsync(StudentCreateDto studentData)
    {
        var connectionString = configuration.GetConnectionString("Default");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var groups = new List<GroupGETDto>();
        if (studentData.GroupAssignments is not null && studentData.GroupAssignments.Count != 0)
        {
            foreach (var group in studentData.GroupAssignments)
            {
                var groupCheckSql = "SELECT * FROM [Group] WHERE Id = @groupId";

                await using var groupCheckCommand = new SqlCommand(groupCheckSql, connection);
                groupCheckCommand.Parameters.AddWithValue("@groupId", group);
                await using var groupCheckReader = await groupCheckCommand.ExecuteReaderAsync();

                if (!await groupCheckReader.ReadAsync())
                {
                    throw new NotFoundException($"Group with id {group} does not exist");
                }

                groups.Add(new GroupGETDto
                {
                    Id = groupCheckReader.GetInt32(0),
                    Name = groupCheckReader.GetString(1),
                });
            }
        }
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var createStudentSql = """
                                   insert into student
                                   output inserted.Id
                                   values (@FirstName, @LastName, @Age);
                                   """;

            await using var createdStudentCommand =
                new SqlCommand(createStudentSql, connection, (SqlTransaction)transaction);
            createdStudentCommand.Parameters.AddWithValue("@FirstName", studentData.FirstName);
            createdStudentCommand.Parameters.AddWithValue("@LastName", studentData.LastName);
            createdStudentCommand.Parameters.AddWithValue("@Age", studentData.Age);

            var createdStudentId = Convert.ToInt32(await createdStudentCommand.ExecuteScalarAsync());
            foreach (var group in groups)
            {
                var groupAssignmentSql = "INSERT INTO GroupAssignment values (@StudentId, @GroupId)";
                await using var groupAssignmentCommand =
                    new SqlCommand(groupAssignmentSql, connection, (SqlTransaction)transaction);
                groupAssignmentCommand.Parameters.AddWithValue("@StudentId", createdStudentId);
                groupAssignmentCommand.Parameters.AddWithValue("@GroupId", group.Id);
                await groupAssignmentCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return new StudentWithGroupDto
            {
                Id = createdStudentId,
                FirstName = studentData.FirstName,
                LastName = studentData.LastName,
                Age = studentData.Age,
                Group = groups
            };
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}