using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace ArchonsToolbox.HelpSystem
{
    public class HelpItem // : INotifyPropertyChanged
    {
        public HelpItem(string Title, string Description)
        {
            this.Title = Title;
            this.Description = Description;
        }

        public string Title 
        { 
            get { return _title; } 
            set { 
                _title = value; 
                //OnPropertyChanged();
            }
        }
        string _title;

        public string Description
        {
            get { return _description; }
            set { 
                _description = value;
                //OnPropertyChanged(); 
            }
        }
        string _description;


        /*public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }*/
    }
}
