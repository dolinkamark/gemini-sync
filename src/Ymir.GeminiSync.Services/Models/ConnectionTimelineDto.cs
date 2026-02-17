using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Ymir.GeminiSync.Services.Models;

public class ConnectionTimelineDto
{
    public int AgreementId { get; set; }

    public bool IsConnectedToGarbage { get; set; }

    public bool IsConnectedToPublicContainer { get; set; }

    public DateTime DateFrom { get; set; }

    public DateTime DateTo { get; set; }
}
