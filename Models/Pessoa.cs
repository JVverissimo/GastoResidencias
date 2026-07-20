using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace project_API.Models
{
    public class Pessoa
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Nome { get; set; } = string.Empty;

        [Required]
        public int Idade { get; set; }

        [Required]
        [MaxLength(11)]
        public string Cpf { get; set; } = string.Empty;
             
        [JsonIgnore]
        public ICollection<Transacao> Transacoes { get; set; } = new List<Transacao>();
    }
}