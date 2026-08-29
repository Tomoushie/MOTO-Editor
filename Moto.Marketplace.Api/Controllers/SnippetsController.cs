// Moto.Marketplace.Api/Controllers/SnippetsController.cs
using Microsoft.AspNetCore.Mvc;
using Moto.Core.Snippets;
using System.Collections.Generic;

namespace Moto.Marketplace.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SnippetsController : ControllerBase
    {
        private readonly ISnippetRepository _repository;

        public SnippetsController(ISnippetRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public ActionResult<IEnumerable<Snippet>> GetAll([FromQuery] string? language = null)
        {
            var snippets = string.IsNullOrEmpty(language)
                ? _repository.GetAll()
                : _repository.GetByLanguage(language);
            return Ok(snippets);
        }

        [HttpPost]
        public ActionResult<Snippet> Create([FromBody] Snippet snippet)
        {
            if (string.IsNullOrWhiteSpace(snippet.Trigger) ||
                string.IsNullOrWhiteSpace(snippet.Body))
            {
                return BadRequest(new { error = "Trigger et Body requis" });
            }

            _repository.Save(snippet);
            return CreatedAtAction(nameof(GetAll), snippet);
        }

        [HttpGet("{id}")]
        public ActionResult<Snippet> GetById(string id)
        {
            var snippet = _repository.GetById(id);
            if (snippet == null)
                return NotFound();
            return Ok(snippet);
        }
    }
}
