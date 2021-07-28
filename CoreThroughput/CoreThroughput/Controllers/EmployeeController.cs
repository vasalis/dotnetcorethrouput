using CoreThroughput.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CoreThroughput.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class EmployeeController : ControllerBase
    {
        private readonly ILogger<EmployeeController> _logger;
        private readonly Container _myDb;

        public EmployeeController(ILogger<EmployeeController> logger, Container aDbContainer)
        {
            _logger = logger;
            _myDb = aDbContainer;
        }

        // GET: api/Employee/Employees
        [HttpPost]
        public async Task<IActionResult> Employees()
        {
            IActionResult returnValue = null;

            try
            {                
                List<EmployeeEntity> lResults = new List<EmployeeEntity>();

                QueryDefinition queryDefinition = new QueryDefinition("select * from Employees");

                using (FeedIterator<EmployeeEntity> feedIterator = this._myDb.GetItemQueryIterator<EmployeeEntity>(
                    queryDefinition,
                    null))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        foreach (var item in await feedIterator.ReadNextAsync())
                        {
                            lResults.Add(item);
                        }
                    }

                    returnValue = new OkObjectResult(lResults);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Could not Get Employees. Exception thrown: {ex.Message}");

                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return returnValue;
        }

        // GET: api/Employee/Employees
        // Bad code practive - this will block the main Thread...
        [HttpPost]
        public IActionResult EmployeesWithWait()
        {
            IActionResult returnValue = null;

            try
            {
                List<EmployeeEntity> lResults = new List<EmployeeEntity>();

                QueryDefinition queryDefinition = new QueryDefinition("select * from Employees");

                using (FeedIterator<EmployeeEntity> feedIterator = this._myDb.GetItemQueryIterator<EmployeeEntity>(
                    queryDefinition,
                    null))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        // Bad code practice - this should be awaited! 
                        // By using .Result it's basically calling .Wait on async method.
                        foreach (var item in feedIterator.ReadNextAsync().Result)
                        {
                            lResults.Add(item);
                        }
                    }

                    // Add this to make it even worse
                    Thread.Sleep(1000);

                    returnValue = new OkObjectResult(lResults);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Could not Get Employees. Exception thrown: {ex.Message}");

                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return returnValue;
        }

        // POST: api/Student
        [HttpPost]
        public async Task NewEmployee([FromBody] string value)
        {
            try
            {
                var lEmployee = JsonConvert.DeserializeObject<EmployeeEntity>(value);

                await _myDb.CreateItemAsync(lEmployee);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Could not create new Employee. Exception thrown: {ex.Message}");
            }           
        }

        // POST: api/Student
        [HttpPost]
        public async Task CreateNewEmployeesBatch()
        {
            try
            {                
                for (int i = 0; i <= 1000; i++)
                {
                    EmployeeEntity lEmployee = new EmployeeEntity()
                    {
                        Id = Guid.NewGuid().ToString(),
                        FirstName = $"first name {1}",
                        LastName = $"last name {1}",
                        DateOfBirth = DateTime.Now
                    };

                    await _myDb.CreateItemAsync(lEmployee);
                }

                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Could not create new Employee. Exception thrown: {ex.Message}");
            }
        }

    }
}
