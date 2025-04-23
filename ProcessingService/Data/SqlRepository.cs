using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcessingService.Configuration;
using ProcessingService.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ProcessingService.Data
{
    public interface ISqlRepository
    {
        Task<int> SaveOrderAsync(OrderData order);
    }

    public class SqlRepository(IOptions<SqlDbSettings> settings, ILogger<SqlRepository> logger)
        : ISqlRepository
    {
        private readonly string _connectionString = settings.Value.ConnectionString;

        public async Task<int> SaveOrderAsync(OrderData order)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Start a transaction to ensure all data is saved atomically
                using var transaction = connection.BeginTransaction();
                
                try
                {
                    // Insert the order header using stored procedure
                    var parameters = new DynamicParameters();
                    parameters.Add("@OrderNumber", order.OrderNumber);
                    
                    var orderId = await connection.QuerySingleAsync<int>(
                        "sp_CreateOrder",
                        parameters,
                        transaction,
                        commandType: CommandType.StoredProcedure);

                    // Commit the transaction if all operations succeed
                    transaction.Commit();

                    logger.LogInformation("Order {OrderNumber} successfully saved with ID {OrderId}", 
                        order.OrderNumber, orderId);
                    
                    return orderId;
                }
                catch (Exception ex)
                {
                    // Roll back the transaction if any operation fails
                    transaction.Rollback();
                    logger.LogError(ex, "Error saving order {OrderNumber} to SQL database", order.OrderNumber);
                    throw; // Rethrow to be handled by the caller
                }
            }
            catch (SqlException ex)
            {
                logger.LogError(ex, "SQL error while saving order {OrderNumber}", order.OrderNumber);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while saving order {OrderNumber}", order.OrderNumber);
                throw;
            }
        }
    }
}