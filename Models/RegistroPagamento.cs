using System;

namespace CSV_GeradorEEditor.Models
{
    public class RegistroPagamento : RegistroPassante
    {
        public string DataPagamento { get; set; }
        public string HoraPagamento { get; set; }
        public double? Acrescimo { get; set; }
        public double? ValorTotal { get; set; }
    }
}