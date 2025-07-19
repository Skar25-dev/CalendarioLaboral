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
            // 7 noches
            Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche,
            // 2 libres
            Turno.Libre, Turno.Libre,
            // 7 tardes
            Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde,
            // 2 libres
            Turno.Libre, Turno.Libre,
            // 7 mañanas
            Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana,
            // 3 libres
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

            #if ANDROID
                SolicitarPermisoEscritura();
            #endif
        }

        #if ANDROID
        private void SolicitarPermisoEscritura()
        {
            var activity = Platform.CurrentActivity;

            if (activity != null)
            {
                var permiso = ContextCompat.CheckSelfPermission(activity, Manifest.Permission.WriteExternalStorage);
                if (permiso != Permission.Granted)
                {
                    ActivityCompat.RequestPermissions(activity, new string[] { Manifest.Permission.WriteExternalStorage }, 0);
                }
            }
        }
        #endif

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
                        Subject = $"Turno {turno}",
                        Background = ColorearTurno(turno)
                    };
                    turnos.Add(nuevo);
                    await GuardarTurnosAsync();
                }
            }
        }

        private static Brush ColorearTurno(Turno turno)
        {
            return turno switch
            {
                Turno.Mañana => FromHex("#99e6bb"),
                Turno.Tarde => FromHex("#80c4a6"),
                Turno.Noche => FromHex("#5c5863"),
                Turno.Libre => FromHex("#a85163"),
                Turno.Vacaciones => FromHex("#ff1f4c"),
                _ => FromHex("#cccccc")
            };
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
                string fecha = turno.StartTime.ToString("yyyy-MM-dd");
                string textoTurno = turno.Subject.Replace("Turno ", "").Trim();
                sb.AppendLine($"{fecha},{textoTurno}");
            }

            string nombreArchivo = $"Turnos_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            string rutaFinal = string.Empty;

            #if ANDROID
                try
                {
                    // Obtiene la ruta de la carpeta pública Documents
                    var rutaDocuments = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "Documents");
                    Directory.CreateDirectory(rutaDocuments); // asegúrate de que exista

                    rutaFinal = Path.Combine(rutaDocuments, nombreArchivo);
                    File.WriteAllText(rutaFinal, sb.ToString());

                    await DisplayAlert("Exportación", $"Archivo guardado en:\n{rutaFinal}", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"No se pudo guardar el archivo:\n{ex.Message}", "OK");
                }
            #elif WINDOWS
            try
            {
                var rutaDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                rutaFinal = Path.Combine(rutaDocuments, nombreArchivo);
                File.WriteAllText(rutaFinal, sb.ToString());

                await DisplayAlert("Exportación", $"Archivo guardado en:\n{rutaFinal}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo guardar en Windows:\n{ex.Message}", "OK");
            }
            #else
                await DisplayAlert("No soportado", "Exportación directa solo disponible en Android o Windows.", "OK");
            #endif
        }


        private Turno ParseTurnoDesdeTexto(string subject)
        {
            return Enum.TryParse<Turno>(subject.Replace("Turno ", ""), out var result) ? result : Turno.Libre;
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
                            Subject = $"Turno {turno.Turno}",
                            Background = ColorearTurno(turno.Turno)
                        };
                        turnos.Add(nuevo);
                    }
                }
            }
        }

        public static Brush FromHex(string hex) => new SolidColorBrush(Microsoft.Maui.Graphics.Color.FromArgb(hex));

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
                MostrarConfigurarTurnoAutomatico();
            }
        }

        private async void MostrarConfigurarTurnoAutomatico()
        {
            var fechaInicio = await DisplayPromptAsync("Inicio", "Introduce la fecha de inicio (YYY-MM-DD):");

            if(!DateTime.TryParse(fechaInicio, out var fecha))
            {
                await DisplayAlert("Error", "Fecha inválida", "OK");
                return;
            }

            string[] patrones = { "Turno Inglés", "7 días trabajo / 2 descanso" };
            var tipo = await DisplayActionSheet("Selecciona patrón", "Cancelar", null, patrones);

            if (tipo == "Turno Inglés")
                GenerarTurnoInglesDesdeFecha(fecha);
            else if (tipo == "5 días trabajo / 2 descanso")
                GenerarTurnoCincoDos(fecha);
        }

        private async void GenerarTurnoInglesDesdeFecha(DateTime fechaInicio)
        {
            const int diasAGenerar = 365;

            for (int i = 0; i < diasAGenerar; i++)
            {
                DateTime fecha = fechaInicio.AddDays(i);
                Turno turno = PatronTurnoIngles28[i % PatronTurnoIngles28.Length];

                var existente = turnos.FirstOrDefault(t => t.StartTime == fecha.Date);
                if (existente != null)
                    turnos.Remove(existente);

                turnos.Add(new SchedulerAppointment
                {
                    StartTime = fecha.AddHours(8),
                    EndTime = fecha.AddHours(16),
                    Subject = $"Turno {turno}",
                    Background = ColorearTurno(turno)
                });
            }
            await GuardarTurnosAsync();
            await DisplayAlert("Turno inglés", "Calendario generado con patrón 28 días (7N-2L-7T-2L-7M-3L).", "OK");
        }

        private async void GenerarTurnoCincoDos(DateTime fechaInicio)
        {
            Turno[] ciclo = { Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Mañana, Turno.Libre, Turno.Libre, 
                              Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Tarde, Turno.Libre, Turno.Libre,
                              Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche, Turno.Noche, Turno.Libre, Turno.Libre };

            DateTime fecha = fechaInicio;
            int dias = 365;

            for (int i = 0; i < dias; i++)
            {
                var turno = ciclo[i % ciclo.Length];
                
                var existente = turnos.FirstOrDefault(t => t.StartTime == fecha.Date);
                if (existente != null)
                    turnos.Remove(existente);

                turnos.Add(new SchedulerAppointment
                {
                    StartTime = fecha.AddHours(8),
                    EndTime = fecha.AddHours(16),
                    Subject = $"Turno {turno}",
                    Background = ColorearTurno(turno)
                });
                fecha = fecha.AddDays(1);
            }
            await GuardarTurnosAsync();
            await DisplayAlert("Turnos generados", "Patrón 5/2 aplicado.", "Ok");
        }

        private async void LimpiarCalendario_Clicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlert("Confirmar", "¿Quieres eliminar todos los turnos?", "Si", "Cancelar");

            if (confirmar)
            {
                turnos.Clear();
                await GuardarTurnosAsync();
                await DisplayAlert("Calendario limpio", "Todos los turnos han sido eliminados.", "Ok");
            }
        }


    }
}
