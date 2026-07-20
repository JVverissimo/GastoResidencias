using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace project_API.Models
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TipoTransacao
    {
        Receita,
        Despesa
    }
    public class Transacao
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Descricao { get; set; } = string.Empty;

        [Required]
        public decimal Valor { get; set; }

        [Required]
        public TipoTransacao Tipo { get; set; }

        public DateTime Data { get; set; } = DateTime.UtcNow;

        [Required]
        public string PessoaCpf { get; set; } = string.Empty;

        [ForeignKey("PessoaCpf")]
        [JsonIgnore] // Impede que o objeto completo seja exigido no corpo do POST do Swagger
        public Pessoa? Pessoa { get; set; }
    }
}