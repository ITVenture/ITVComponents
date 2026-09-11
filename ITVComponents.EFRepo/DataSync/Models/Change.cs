using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.DataSync.Models
{
    public class Change
    {
        public string EntityName { get; set; }
        public Dictionary<string,string> Key { get; set; }
        public Dictionary<string,string> KeyExpression { get; set; }
        public ChangeType ChangeType { get; set; }

        public List<ChangeDetail> Details { get; set; } = new List<ChangeDetail>();

        public bool Apply { get; set; }

        public int DeletePriority { get; set; } = -1;

        /// <summary>
        /// Die Sektion, aus der dieser Change stammt - null fuer die Sektionen des Host-Kontexts.
        /// </summary>
        /// <remarks>
        /// Gesetzt wird er zentral beim Vergleich, nicht von der Extension selbst. Er entscheidet beim
        /// Einspielen, GEGEN WELCHEN DbContext der Change laeuft: eine Sektion, deren Entitaeten in
        /// einem eigenen Kontext liegen, waere im Host-Kontext nicht einmal aufloesbar.
        ///
        /// Eine Eigenschaft am Change und keine Gruppierung daneben, weil der MVC-Weg die Changes als
        /// JSON zum Client und zurueck reicht - eine Zuordnung, die nur im Speicher lebt, ueberlebt
        /// das nicht.
        /// </remarks>
        public string SectionKey { get; set; }
    }

    public enum ChangeType
    {
        Insert,
        Update,
        Delete,
        Info,
        Warning
    }
}
