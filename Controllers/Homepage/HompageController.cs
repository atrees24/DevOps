using AutoMapper;
using developers.Data;
using developers.DTOs;
using developers.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace developers.Controllers
{
    // [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class HomepageController : ControllerBase
    {
        private readonly IDataRepository<ProjectDeveloper> _projectDeveloperRepository;
        private readonly IDataRepository<Developer> _developerRepository;
        private readonly IWebHostEnvironment _hostingEnvironment;


        private readonly IDataRepository<TaskDeveloper> _taskDeveloperRepository;


        public HomepageController(IDataRepository<ProjectDeveloper> projectDeveloperRepository, 
                        IDataRepository<Developer> developerRepository,IDataRepository<TaskDeveloper> taskDeveloperRepository , IWebHostEnvironment hostingEnvironment)
        {
            _projectDeveloperRepository = projectDeveloperRepository;
            _developerRepository = developerRepository;
            _taskDeveloperRepository = taskDeveloperRepository;
            _hostingEnvironment = hostingEnvironment;

        }

        [HttpGet("projects/{userId}")]
        public async Task<ActionResult<IEnumerable<Project>>> GetProjectsForDeveloper(int userId)
        {
            try
            {
                var developer = await _developerRepository.GetContext().Developers
                    .FirstOrDefaultAsync(d => d.UserID == userId);

                if (developer == null)
                    return NotFound("Developer not found.");

                // Retrieve projects associated with the DeveloperID
                var projects = await (from pd in _projectDeveloperRepository.GetContext().ProjectDevelopers
                                    join p in _projectDeveloperRepository.GetContext().Projects on pd.ProjectID equals p.ID
                                    where pd.DeveloperID == developer.ID
                                    select p).ToListAsync();

                return Ok(projects);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }




[HttpPost("AcceptProject")]
public async Task<IActionResult> AcceptProject([FromBody] ProjectDeveloper projectDeveloper)
{
    try
    {
        Console.WriteLine($"AcceptProject request received with ProjectID: {projectDeveloper.ProjectID}, DeveloperID: {projectDeveloper.DeveloperID}");

        var existingProjectDeveloper = await _projectDeveloperRepository.GetContext().ProjectDevelopers
            .FirstOrDefaultAsync(pd => pd.ProjectID == projectDeveloper.ProjectID && pd.DeveloperID == projectDeveloper.DeveloperID && pd.Accepted == "Accepted");

        if (existingProjectDeveloper != null)
            return BadRequest("Project has already been accepted by the developer.");

        // Retrieve the ProjectDeveloper entity using LINQ query
        existingProjectDeveloper = await _projectDeveloperRepository.GetContext().ProjectDevelopers
            .FirstOrDefaultAsync(pd => pd.ProjectID == projectDeveloper.ProjectID && pd.DeveloperID == projectDeveloper.DeveloperID);

        // Check if the entity exists
        if (existingProjectDeveloper == null)
            return NotFound("Project developer not found.");

        // Update the 'Accepted' status
        existingProjectDeveloper.Accepted = "Accepted";
        await _projectDeveloperRepository.GetContext().SaveChangesAsync();

        // Fetch the corresponding project
        var project = await _projectDeveloperRepository.GetContext().Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.ID == projectDeveloper.ProjectID);

        // Check if the project exists
        if (project == null)
            return NotFound("Project not found.");

        // Assign tasks to the developer
        foreach (var task in project.Tasks)
        {
            var taskDeveloper = new TaskDeveloper
            {
                TaskId = task.Id,
                DeveloperId = projectDeveloper.DeveloperID,
                Status = "in progress"
            };
            _projectDeveloperRepository.GetContext().TaskDevelopers.Add(taskDeveloper);
        }
        await _projectDeveloperRepository.GetContext().SaveChangesAsync();

        return Ok("Project accepted and tasks assigned.");
    }
    catch (Exception ex)
    {
        return StatusCode(500, $"An error occurred: {ex.Message}");
    }
}


 [HttpGet("tasks/{developerId}")]
        public async Task<ActionResult<IEnumerable<TaskUserViewModel>>> GetTaskDetailsByDeveloper(int developerId)
        {
            try
            {
                var taskDetails = await _taskDeveloperRepository.GetContext().TaskDevelopers
                    .Where(td => td.DeveloperId == developerId)
                    .Include(td => td.Task)
                        .ThenInclude(t => t.Project)
                    .Select(td => new TaskUserViewModel
                    {
                        TaskId = td.TaskId,
                        TaskTitle = td.Task.Title,
                        TaskStatus = td.Status,
                        ProjectName = td.Task.Project.Name,
                        TaskDescription = td.Task.Description,
                        TaskImage = td.Task.ImageFilePath
                    })
                    .ToListAsync();

                return taskDetails;
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }



        [HttpGet("alltasks")]
        public async Task<ActionResult<IEnumerable<TaskDeveloperViewModel>>> GetAssignedTasks()
        {
            try
            {
                var taskDeveloperViewModels = await _taskDeveloperRepository.GetContext().TaskDevelopers
                    .Include(td => td.Task)
                        .ThenInclude(t => t.Project)
                    .Include(td => td.Developer)
                        .ThenInclude(d => d.user) // Assuming there's a navigation property from Developer to User
                    .Select(td => new TaskDeveloperViewModel
                    {
                        TaskId = td.TaskId,
                        DeveloperId = td.DeveloperId,
                        ProjectId = td.Task.ProjectID,
                        DeveloperName = td.Developer.user.Name,
                        TaskName = td.Task.Title,
                        ProjectName = td.Task.Project.Name,
                        Status = td.Status,
                        taskImage = td.Task.ImageFilePath

                    })
                    .ToListAsync();

                return taskDeveloperViewModels;
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


[HttpPut("taskSubmission/{taskId}/{developerId}")]
public async Task<IActionResult> UpdateTaskDeveloper(int taskId, int developerId, [FromForm] TaskDeveloper model)
{
    try
    {
        if (model.File != null)
        {
            // Check file extension
            string fileExtension = Path.GetExtension(model.File.FileName).ToLower();
            if (fileExtension != ".pdf")
            {
                return BadRequest("Invalid file format. Please upload a .pdf file.");
            }

            // Get the wwwroot directory
            var wwwRootPath = _hostingEnvironment.WebRootPath;

            // Combine wwwroot directory with the uploads folder and file name
            var uploadsDirectory = Path.Combine(wwwRootPath, "uploads");
            var fileName = Guid.NewGuid().ToString() + fileExtension;
            var filePath = Path.Combine(uploadsDirectory, fileName);

            // Create the uploads directory if it doesn't exist
            if (!Directory.Exists(uploadsDirectory))
            {
                Directory.CreateDirectory(uploadsDirectory);
            }

            // Save the file to the specified path
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.File.CopyToAsync(stream);
            }

            // Find the task developer by taskId and developerId
            var taskDeveloper = await _taskDeveloperRepository.GetContext().TaskDevelopers
                .FirstOrDefaultAsync(td => td.TaskId == taskId && td.DeveloperId == developerId);

            // If task developer is not found, return not found error
            if (taskDeveloper == null)
            {
                return NotFound("Task developer not found.");
            }

            // Update the file path in the database
            taskDeveloper.FilePath = Path.Combine("uploads", fileName); // Relative path from wwwroot

            // Save changes to the database
            await _taskDeveloperRepository.GetContext().SaveChangesAsync();
        }

        // Return success message
        return Ok("Task developer updated successfully.");
    }
    catch (Exception ex)
    {
        // Return internal server error if any exception occurs
        return StatusCode(500, $"An error occurred: {ex.Message}");
    }
}

















    }
}




    

    



