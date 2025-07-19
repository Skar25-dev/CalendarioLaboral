using Microsoft.Maui.ApplicationModel.DataTransfer;
using Syncfusion.Maui.Scheduler;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
#if ANDROID
using Android;
using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;
#endif

namespace AppCalendarioLaboral
{
    public partial class MainPage : ContentPage
    {
        private ObservableCollection<SchedulerAppointment> turnos = new();
        private const string NombreArchivo = "turnos.json";
        private readonly string rutaArchivo = Path.Combine(FileSystem.AppDataDirectory, NombreArchivo);
        private string rutaUltimoArchivoExportado;
        private bool modoAutomatico = false;
        private readonly Turno[] PatronTurnoIngles28 = new Turno[]
        {
            Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche,
            Turno.Libre, Turno.Libre,
            Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde,
            Turno.Libre, Turno.Libre,
            Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana,
            Turno.Libre, Turno.Libre, Turno.Libre
        };


        public MainPage()
        {
            InitializeComponent();
            Calendario.AppointmentsSource = turnos;
            _ = CargarTurnosAsync();

        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
        }

        private async void Calendario_Tapped(object sender, SchedulerTappedEventArgs e)
        {
            if (e.Date != null)
            {
                var fecha = e.Date.Value.Date;

                var turnoExistente = turnos.FirstOrDefault(t => t.StartTime.Date == fecha);

                List<string> opciones = new() { "Mañana", "Tarde", "Noche", "Libre", "Vacaciones" };

                if (turnoExistente != null)
                {
                    opciones.Add("Eliminar turno");
                }

                string opcion = await DisplayActionSheet(
                    $"Gestionar turno para el {fecha:dd/MM/yyyy}",
                    "Cancelar",
                    null,
                    opciones.ToArray());

                if (!string.IsNullOrEmpty(opcion) && opcion != "Cancelar")
                {
                    if (opcion == "Eliminar turno")
                    {
                        if (turnoExistente != null)
                        {
                            turnos.Remove(turnoExistente);
                            await GuardarTurnosAsync();
                        }
                        return;
                    }

                    Turno turno = opcion switch
                    {
                        "Mañana" => Turno.Mañana,
                        "Tarde" => Turno.Tarde,
                        "Noche" => Turno.Noche,
                        "Libre" => Turno.Libre,
                        "Vacaciones" => Turno.Vacaciones,
                        _ => Turno.Libre,
                    };

                    if (turnoExistente != null)
                        turnos.Remove(turnoExistente);

                    var nuevo = new SchedulerAppointment
                    {
                        StartTime = fecha.AddHours(8),
                        EndTime = fecha.AddHours(16),
                        Subject = $"{turno}",
                        Background = turno switch
                        {
                            Turno.Mañana => FromHex("#3F04BF"),
                            Turno.Tarde => FromHex("#9980F2"),
                            Turno.Noche => FromHex("#D92525"),
                            Turno.Libre => FromHex("#43D978"),
                            Turno.Vacaciones => FromHex("#F7B6F6"),
                            _ => FromHex("#cccccc")
                        }
                    };
                    turnos.Add(nuevo);
                    await GuardarTurnosAsync();
                }
            }
        }

        private void VerResumenMensual_Clicked(object sender, EventArgs e)
        {
            var mesActual = DateTime.Now.Month;
            var anioActual = DateTime.Now.Year;

            var delMes = turnos.Where(t => t.StartTime.Month == mesActual && t.StartTime.Year == anioActual).ToList();

            if (!delMes.Any())
            {
                DisplayAlert("Resumen mensual", "No hay turnos asignados este mes.", "Ok");
                return;
            }

            var resumen = delMes.GroupBy(t => t.Subject).Select(g => $"{g.Key}: {g.Count()} día(s)").ToList();
            
            string mensaje = string.Join(Environment.NewLine, resumen);

            DisplayAlert("Resumen mensual", mensaje, "Ok");
        }

        private async void ExportarTurnos_Clicked(object sender, EventArgs e)
        {
            if (!turnos.Any())
            {
                await DisplayAlert("Exportación", "No hay turnos para exportar.", "Ok");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Fecha,Turno");

            foreach (var turno in turnos.OrderBy(t => t.StartTime))
            {
                sb.AppendLine($"{turno.StartTime:yyyy-MM-dd},{turno.Subject}");
            }

            var nombreArchivo = $"Turnos_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            var ruta = Path.Combine(FileSystem.AppDataDirectory, nombreArchivo);
            File.WriteAllText(ruta, sb.ToString());

            rutaUltimoArchivoExportado = ruta;

            await DisplayAlert("Exportación completada", $"Archivo guardado en:\n{ruta}", "OK");
        }


        private Turno ParseTurnoDesdeTexto(string subject)
        {
            return Enum.TryParse<Turno>(subject, out var result) ? result : Turno.Libre;
        }


        private async Task GuardarTurnosAsync()
        {
            var lista = turnos.Select(t => new TurnoGuardado
            {
                Fecha = t.StartTime.Date,
                Turno = ParseTurnoDesdeTexto(t.Subject)
            }).ToList();

            var json = JsonSerializer.Serialize(lista);
            await File.WriteAllTextAsync(rutaArchivo, json);
        }

        private async Task CargarTurnosAsync()
        {
            if (File.Exists(rutaArchivo))
            {
                var json = await File.ReadAllTextAsync(rutaArchivo);
                var lista = JsonSerializer.Deserialize<List<TurnoGuardado>>(json);

                if (lista != null)
                {
                    foreach (var turno in lista)
                    {
                        var nuevo = new SchedulerAppointment
                        {
                            StartTime = turno.Fecha.AddHours(8),
                            EndTime = turno.Fecha.AddHours(16),
                            Subject = $"{turno.Turno}",
                            Background = turno.Turno switch
                            {
                                Turno.Mañana => FromHex("#3F04BF"),
                                Turno.Tarde => FromHex("#9980F2"),
                                Turno.Noche => FromHex("#D92525"),
                                Turno.Libre => FromHex("#43D978"),
                                Turno.Vacaciones => FromHex("#F7B6F6"),
                                _ => FromHex("#cccccc")
                            }
                        };
                        turnos.Add(nuevo);
                    }
                }
            }
        }

        public static Brush FromHex(string hex) => new SolidColorBrush(Microsoft.Maui.Graphics.Color.FromArgb(hex));

        private async void LimpiarCalendario_Clicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert("Confirmar", "¿Quieres eliminar todos los turnos?", "Sí", "Cancelar");

            if (confirmar)
            {
                turnos.Clear();
                await GuardarTurnosAsync();
                await DisplayAlert("Calendario limpio", "Todos los turnos han sido eliminados.", "OK");
            }
        }


        private async void CompartirArchivo_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(rutaUltimoArchivoExportado) || !File.Exists(rutaUltimoArchivoExportado))
            {
                await DisplayAlert("Compartir", "No hay archivo exportado aún.", "Ok");
                return;
            }

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Compartir archivo de turnos",
                File = new ShareFile(rutaUltimoArchivoExportado)
            });
        }


        private void ModoPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            modoAutomatico = ModoPicker.SelectedIndex == 1;

            if (modoAutomatico)
            {
                MostrarConfiguradorTurnoAutomatico();
            }
        }

        private async void MostrarConfiguradorTurnoAutomatico()
        {
            var fechaInicio = await DisplayPromptAsync("Inicio", "Introduce la fecha de inicio (YYYY-MM-DD):");

            if (!DateTime.TryParse(fechaInicio, out var fecha))
            {
                await DisplayAlert("Error", "Fecha inválida.", "OK");
                return;
            }

            string[] patrones = { "Turno Inglés", "5 días trabajo / 2 descanso" };
            var tipo = await DisplayActionSheet("Selecciona patrón", "Cancelar", null, patrones);

            if (tipo == "Turno Inglés")
                GenerarTurnoInglesDesde(fecha);
            else if (tipo == "5 días trabajo / 2 descanso")
                GenerarTurnoCincoDos(fecha);
        }

        private async void GenerarTurnoInglesDesde(DateTime fechaInicio)
        {
            const int diasAGenerar = 365;

            for (int i = 0; i < diasAGenerar; i++)
            {
                DateTime fecha = fechaInicio.AddDays(i);
                Turno turno = PatronTurnoIngles28[i % PatronTurnoIngles28.Length];

                var existente = turnos.FirstOrDefault(t => t.StartTime.Date == fecha.Date);
                if (existente != null)
                    turnos.Remove(existente);

                turnos.Add(new SchedulerAppointment
                {
                    StartTime = fecha.AddHours(8),
                    EndTime = fecha.AddHours(16),
                    Subject = $"{turno}",
                    Background = turno switch
                    {
                        Turno.Mañana => FromHex("#3F04BF"),
                        Turno.Tarde => FromHex("#9980F2"),
                        Turno.Noche => FromHex("#D92525"),
                        Turno.Libre => FromHex("#43D978"),
                        Turno.Vacaciones => FromHex("#F7B6F6"),
                        _ => FromHex("#cccccc")
                    }
                });
            }

            await GuardarTurnosAsync();
            await DisplayAlert("Turno Inglés", "Calendario generado con patrón 28 días (7N-2L-7T-2L-7M-3L).", "OK");
        }

        private async void GenerarTurnoCincoDos(DateTime fechaInicio)
        {
            Turno[] ciclo = { Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Libre, Turno.Libre,
                              Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Libre, Turno.Libre,
                              Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche, Turno.Libre, Turno.Libre};

            DateTime fecha = fechaInicio;
            int dias = 365;

            for (int i = 0; i < dias; i++)
            {
                var turno = ciclo[i % ciclo.Length];

                var existente = turnos.FirstOrDefault(t => t.StartTime.Date == fecha.Date);
                if (existente != null)
                    turnos.Remove(existente);

                turnos.Add(new SchedulerAppointment
                {
                    StartTime = fecha.AddHours(8),
                    EndTime = fecha.AddHours(16),
                    Subject = $"{turno}",
                    Background = turno switch
                    {
                        Turno.Mañana => FromHex("#3F04BF"),
                        Turno.Tarde => FromHex("#9980F2"),
                        Turno.Noche => FromHex("#D92525"),
                        Turno.Libre => FromHex("#43D978"),
                        Turno.Vacaciones => FromHex("#F7B6F6"),
                        _ => FromHex("#cccccc")
                    }
                });

                fecha = fecha.AddDays(1);
            }

            await GuardarTurnosAsync();
            await DisplayAlert("Turnos generados", "Patrón 5/2 aplicado.", "OK");
        }

    }
}
