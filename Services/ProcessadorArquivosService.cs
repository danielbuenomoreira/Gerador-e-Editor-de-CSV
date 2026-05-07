using System;
using System.Collections.Generic;
using System.Linq;
using CSV_GeradorEEditor.Models;

namespace CSV_GeradorEEditor.Services
{
    public class ProcessadorArquivosService
    {
        public List<RegistroPassante> ProcessarDaer(List<RegistroPassante> daerNaoFiltrado, List<RegistroPassante> rejeitados)
        {
            // AsParallel divide a carga da subtração de conjuntos entre os núcleos do processador
            return daerNaoFiltrado.AsParallel().Except(rejeitados.AsParallel()).ToList();
        }

        public List<T> FiltrarPorData<T>(List<T> registros, string colunaData, DateTime dataInicio, DateTime dataFim)
        {
            return registros.AsParallel().Where(r =>
            {
                var propriedade = typeof(T).GetProperty(colunaData);
                if (propriedade == null) return false;

                string valorData = propriedade.GetValue(r)?.ToString();
                if (DateTime.TryParseExact(valorData, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime dataConvertida))
                {
                    return dataConvertida >= dataInicio && dataConvertida <= dataFim;
                }
                return false;
            }).ToList();
        }
    }
}