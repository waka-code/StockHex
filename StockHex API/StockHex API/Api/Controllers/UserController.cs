using Microsoft.AspNetCore.Mvc;
using StockHex_API.Application.UseCases.UserUseCases;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly PostUser _postUser;
        private readonly UpdateUser _updateUseryById;
        private readonly DeleteUserById _deleteUserById;
        private readonly GetUserById _getUserById;
        private readonly GetAllUser _getAllUser;

        public UserController(
     PostUser postUser,
     UpdateUser updateUser,
     DeleteUserById deleteUserById,
     GetUserById getUserById,
     GetAllUser getAllUser)
        {
            _postUser = postUser;
            _updateUseryById = updateUser;
            _deleteUserById = deleteUserById;
            _getUserById = getUserById;
            _getAllUser = getAllUser;
        }

        [HttpGet]
        [Route("${id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _getUserById.RunAsync(id);
            return Ok(user);
        }

        [HttpGet]
        [Route("all")]
        public async Task<IActionResult> GetAll()
        {
            var user = await _getAllUser.Run();
            return Ok(user);
        }

        [HttpPost]
        [Route("new")]
        public async Task<IActionResult> Create([FromBody] User user)
        {
            await _postUser.Run(user);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }

        [HttpPut]
        [Route("update")]
        public async Task<IActionResult> Update(Guid id, [FromBody] User user)
        {
            user.Id = id;
            await _updateUseryById.Run(user);
            return NoContent();
        }

        [HttpDelete]
        [Route("delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _deleteUserById.Run(id);
            return NoContent();
        }
    }
}
