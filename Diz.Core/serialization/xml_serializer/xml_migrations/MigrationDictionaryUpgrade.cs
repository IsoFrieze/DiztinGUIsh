using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using ExtendedXmlSerializer;

namespace Diz.Core.serialization.xml_serializer.xml_migrations
{
    public class DizProjectMigrations : IEnumerable<Action<XElement>>
    {
        public static void MigrationV0(XElement node)
        {
            /*var typeElement = node.Member("sys:Item");
            if (typeElement == null)
                return;*/

            int x = 3;
            // Add new node
            // node.Add(new XElement("Name", typeElement.Value));
            // Remove old node
            // typeElement.Remove();
        }
    
        public static void MigrationV1(XElement node)
        {
            // Add new node
            // node.Add(new XElement("Value", "Calculated"));
        }
    
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
        public IEnumerator<Action<XElement>> GetEnumerator()
        {
            yield return MigrationV0;
            // yield return MigrationV1;
        }
    }
}