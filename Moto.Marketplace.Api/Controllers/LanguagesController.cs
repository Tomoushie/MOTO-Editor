// Moto.Marketplace.Api/Controllers/LanguagesController.cs
using Microsoft.AspNetCore.Mvc;
using Moto.Core.I18n;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Marketplace.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class LanguagesController : ControllerBase
    {
        private readonly ILanguagePackRepository _repository;
        private readonly ISignatureVerifier _signatureVerifier;

        public LanguagesController(
            ILanguagePackRepository repository,
            ISignatureVerifier signatureVerifier)
        {
            _repository = repository;
            _signatureVerifier = signatureVerifier;
        }

        /// <summary>
        /// GET /api/v1/languages
        /// Liste tous les packs de langues disponibles.
        /// </summary>
        [HttpGet]
        public ActionResult<IEnumerable<LanguagePackInfo>> GetAll()
        {
            var packs = _repository.GetAll();
            return Ok(packs);
        }

        /// <summary>
        /// GET /api/v1/languages/{code}
        /// Obtient un pack spécifique.
        /// </summary>
        [HttpGet("{code}")]
        public ActionResult<LanguagePack> GetByCode(string code)
        {
            var pack = _repository.GetByCode(code);
            if (pack == null)
                return NotFound();
            return Ok(pack);
        }

        /// <summary>
        /// POST /api/v1/languages
        /// Soumet un nouveau pack (validation signature automatique).
        /// </summary>
        [HttpPost]
        public ActionResult<LanguagePack> Submit([FromBody] LanguagePackSubmission submission)
        {
            // 1. Vérifier la signature
            if (!_signatureVerifier.Verify(submission.PackJson, submission.Signature))
            {
                return BadRequest(new { error = "Signature invalide" });
            }

            // 2. Valider le pack
            var pack = System.Text.Json.JsonSerializer.Deserialize<LanguagePack>(submission.PackJson);
            if (pack == null)
                return BadRequest(new { error = "Pack invalide" });

            // 3. Sauvegarder
            _repository.Save(pack);

            return CreatedAtAction(nameof(GetByCode), new { code = pack.Id }, pack);
        }

        /// <summary>
        /// POST /api/v1/languages/translate
        /// Traduit un pack via l'API IA (optionnel).
        /// </summary>
        [HttpPost("translate")]
        public async Task<ActionResult<LanguagePack>> Translate([FromBody] TranslationRequest request)
        {
            // En production : appeler AiTranslationEngine
            // Pour la démo : retourner un placeholder
            return Ok(new LanguagePack
            {
                Id = request.TargetLanguage,
                Name = $"Traduction {request.TargetLanguage}",
                Translations = new Dictionary<string, string>()
            });
        }
    }

    public class LanguagePackSubmission
    {
        public string PackJson { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string AuthorEmail { get; set; } = string.Empty;
    }

    public class TranslationRequest
    {
        public string SourceLanguage { get; set; } = string.Empty;
        public string TargetLanguage { get; set; } = string.Empty;
        public string PackJson { get; set; } = string.Empty;
    }
}
