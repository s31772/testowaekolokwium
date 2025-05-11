using Microsoft.AspNetCore.Mvc;
using TEST2kolokwiumAPBD.DTOs;
using TEST2kolokwiumAPBD.Exceptions;

namespace TEST2kolokwiumAPBD.Controllers;

[ApiController]
[Route("[controller]")]
public class StudentController(IDBService service) : ControllerBase
{
    ///student?firstName=Ania
    [HttpGet]
    public async Task<ActionResult> GetAllStudents([FromQuery] string? firstName)
    {
        try
        {
            var students = await service.GetAllStudentsAsync(firstName);
            return Ok(students);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
    [HttpPost]
    public async Task<IActionResult> CreateStudent([FromBody] StudentCreateDto body)
    {
        try
        {
            var student = await service.CreateStudentAsync(body);
            return Created($"students/{student.Id}", student);
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
}