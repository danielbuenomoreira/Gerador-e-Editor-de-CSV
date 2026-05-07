using System;

namespace CSV_GeradorEEditor.Models
{
    public class RegistroPassante : IEquatable<RegistroPassante>
    {
        public string Placa { get; set; }
        public string Data { get; set; }
        public string Hora { get; set; }
        public string Portico { get; set; }
        public int Categoria { get; set; }
        public string Sentido { get; set; }
        public double Tarifa { get; set; }

        // Utilizado para subtrair listas (DAER = DAERnaoFiltrado - REJEITADOS)
        public bool Equals(RegistroPassante? other)
        {
            if (other == null) return false;
            return Placa == other.Placa && Data == other.Data && Hora == other.Hora;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Placa, Data, Hora);
        }
    }
}