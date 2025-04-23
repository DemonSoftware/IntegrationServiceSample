using ReceiverService.Models;

namespace ReceiverService.Services
{
    public interface IReceiverService
    {
        Task<ProcessingResult> ProcessJsonRequestAsync(JsonRequest request);
    }
    
    public class ProcessingResult
    {
        public string RequestId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorDetails { get; set; }
    }
}