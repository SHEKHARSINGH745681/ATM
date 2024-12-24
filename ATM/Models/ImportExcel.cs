using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ATM.Models
{
    public class ImportExcel
    {
        [Key]
       // [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Age { get; set; }
        public string? PINCODE { get; set; }


    }
}
