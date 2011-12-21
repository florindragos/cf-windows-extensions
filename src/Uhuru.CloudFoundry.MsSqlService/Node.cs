﻿// -----------------------------------------------------------------------
// <copyright file="Node.cs" company="Uhuru Software, Inc.">
// Copyright (c) 2011 Uhuru Software, Inc., All Rights Reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Uhuru.CloudFoundry.MSSqlService
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Transactions;
    using Uhuru.CloudFoundry.ServiceBase;
    using Uhuru.Utilities;
    
    /// <summary>
    /// This class is the MS SQL Server Node that brings this RDBMS as a service to Cloud Foundry.
    /// </summary>
    public partial class Node : NodeBase
    {
        private const int KEEP_ALIVE_INTERVAL = 15000;
        private const int LONG_QUERY_INTERVAL = 1;
        private const int STORAGE_QUOTA_INTERVAL = 1;

        private MSSqlOptions mssqlConfig;
        private int maxDbSize;
        private int maxLongQuery;
        private int maxLongTx;
        SqlConnection connection;
        private string baseDir;
        private int availableStorage;
        private int nodeCapacity;
        private int queriesServed;
        private DateTime qpsLastUpdated;
        private int longQueriesKilled;
        private int longTxKilled;
        private int provisionServed;
        private int bindingServed;
        private string localIp;
        
        /// <summary>
        /// Gets the service name.
        /// </summary>
        /// <returns>
        /// "MssqlaaS"
        /// </returns>
        protected override string ServiceName()
        {
            return "MssqlaaS";
        }

        /// <summary>
        /// Starts the node.
        /// </summary>
        /// <param name="options">The configuration options for the node.</param>
        public override void Start(Options options)
        {
            base.Start(options);
        }

        /// <summary>
        /// Starts the node.
        /// </summary>
        /// <param name="options">The configuration options for the node.</param>
        /// <param name="msSqlOptions">The MS SQL Server options.</param>
        public void Start(Options options, MSSqlOptions msSqlOptions)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }
            if (msSqlOptions == null)
            {
                throw new ArgumentNullException("msSqlOptions");
            }

            this.mssqlConfig = msSqlOptions;
            this.maxDbSize = options.MaxDBSize * 1024 * 1024;
            this.maxLongQuery = options.MaxLengthyQuery;
            this.maxLongTx = options.MaxLengthyTX;
            this.localIp = NetworkInterface.GetLocalIPAddress(options.LocalRoute);

            this.connection = mssql_connect();

            TimerHelper.RecurringCall(KEEP_ALIVE_INTERVAL, delegate()
            {
                mssql_keep_alive();
            });

            if (maxLongQuery > 0)
            {
                TimerHelper.RecurringCall(maxLongQuery / 2, delegate()
                {
                    kill_long_queries();
                });
            }

            if (this.maxLongTx > 0)
            {
                TimerHelper.RecurringCall(this.maxLongTx / 2, delegate()
                {
                    mssql_keep_alive();
                });
            }

            TimerHelper.RecurringCall(STORAGE_QUOTA_INTERVAL, delegate()
            {
                enforce_storage_quota();
            });

            this.baseDir = options.BaseDir;
            if (!string.IsNullOrEmpty(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            ProvisionedService.Initialize(options.LocalDB);

            check_db_consistency();

            this.availableStorage = options.AvailableStorage * 1024 * 1024;
            this.nodeCapacity = this.availableStorage;

            foreach (ProvisionedService provisioned_service in ProvisionedService.GetInstances())
            {
                this.availableStorage -= storage_for_service(provisioned_service);
            }

            this.queriesServed = 0;
            this.qpsLastUpdated = DateTime.Now;
            // initialize qps counter
            this.get_qps();
            this.longQueriesKilled = 0;
            this.longTxKilled = 0;
            this.provisionServed = 0;
            this.bindingServed = 0;
            this.Start(options);

        }

        /// <summary>
        /// Gets any service-specific announcement details.
        /// </summary>
        protected override Announcement AnnouncementDetails
        {
            get
            {
                Announcement a = new Announcement();
                a.AvailableStorage = this.availableStorage;
                return a;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        private void check_db_consistency()
        {
            // method present in mysql and postgresql
            //todo: vladi: this should be replaced with ms sql server code
        }

        private int storage_for_service(ProvisionedService provisioned_service)
        {
            switch (provisioned_service.Plan)
            {
                case ProvisionedServicePlanType.Free: return maxDbSize;
                default: throw new MSSqlError(MSSqlError.MSSqlInvalidPlan, provisioned_service.Plan.ToString());
            }
        }

        private string ConnectionString
        {
            get
            {
                return String.Format(CultureInfo.InvariantCulture, Strings.SqlNodeConnectionString, 
                    mssqlConfig.Host, mssqlConfig.Port, mssqlConfig.User, mssqlConfig.Password);
            }
        }

        private SqlConnection mssql_connect()
        {
            for (int i = 0; i < 5; i++)
            {
                connection = new SqlConnection(ConnectionString);

                try
                {
                    connection.Open();
                    return connection;
                }
                catch (InvalidOperationException)
                {
                }
                catch (SqlException)
                {
                }
                Thread.Sleep(5000);
            }

            Logger.Fatal(Strings.SqlNodeConnectionUnrecoverableFatalMessage);
            Shutdown();
            Process.GetCurrentProcess().Kill();
            return null;
        }

        //keep connection alive, and check db liveness
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        private void mssql_keep_alive()
        {
            // present in both mysql and postgresql
            try
            {
                using (SqlCommand cmd = new SqlCommand(Strings.SqlNodeKeepAliveSQL, connection))
                {
                    cmd.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(Strings.SqlNodeConnectionLostWarningMessage, ex.ToString());
                connection = mssql_connect();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        private void kill_long_queries()
        {
            //present in both mysql and postgresql
            //todo: vladi: implement this
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        private void kill_long_transaction()
        {
            //present in both mysql and postgresql
            //todo: vladi: implement this
        }

        /// <summary>
        /// Provisions an MS Sql Server database.
        /// </summary>
        /// <param name="plan">The payment plan for the service.</param>
        /// <returns>
        /// Credentials for the provisioned service.
        /// </returns>
        protected override ServiceCredentials Provision(ProvisionedServicePlanType plan)
        {
            return Provision(plan, null);
        }

        /// <summary>
        /// Provisions an MS Sql Server database.
        /// </summary>
        /// <param name="plan">The payment plan for the service.</param>
        /// <param name="credentials">Existing credentials for the service.</param>
        /// <returns>
        /// Credentials for the provisioned service.
        /// </returns>
        protected override ServiceCredentials Provision(ProvisionedServicePlanType plan, ServiceCredentials credentials)
        {
            ProvisionedService provisioned_service = new ProvisionedService();
            try
            {
                if (credentials != null)
                {
                    string name = credentials.Name;
                    string user = credentials.User;
                    string password = credentials.Password;
                    provisioned_service.Name = name;
                    provisioned_service.User = user;
                    provisioned_service.Password = password;
                }
                else
                {
                    // mssql database name should start with alphabet character
                    provisioned_service.Name = "D4Ta" + Guid.NewGuid().ToString("N");
                    provisioned_service.User = "US3r" + Credentials.GenerateCredential();
                    provisioned_service.Password = "P4Ss" + Credentials.GenerateCredential();

                }
                provisioned_service.Plan = plan;

                create_database(provisioned_service);

                if (!ProvisionedService.Save())
                {
                    Logger.Error(Strings.SqlNodeCannotSaveProvisionedServicesErrorMessage, provisioned_service.SerializeToJson());
                    throw new MSSqlError(MSSqlError.MSSqlLocalDBError);
                }

                ServiceCredentials response = gen_credential(provisioned_service.Name, provisioned_service.User, provisioned_service.Password);
                provisionServed += 1;
                return response;
            }
            catch (Exception)
            {
                delete_database(provisioned_service);
                throw;
            }
        }

        /// <summary>
        /// Unprovisions a SQL Server database.
        /// </summary>
        /// <param name="name">The name of the service to unprovision.</param>
        /// <param name="bindings">Array of bindings for the service that have to be unprovisioned.</param>
        /// <returns>
        /// A boolean specifying whether the unprovision request was successful.
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        protected override bool Unprovision(string name, ServiceCredentials[] bindings)
        {
            if (String.IsNullOrEmpty(name))
            {
                return false;
            }

            Logger.Debug(Strings.SqlNodeUnprovisionDatabaseDebugMessage, name, JsonConvertibleObject.SerializeToJson((bindings.Select(binding => binding.ToJsonIntermediateObject()).ToArray())));

            ProvisionedService provisioned_service = ProvisionedService.GetService(name);

            if (provisioned_service == null)
            {
                throw new MSSqlError(MSSqlError.MSSqlConfigNotFound, name);
            }

            // TODO: validate that database files are not lingering
            // Delete all bindings, ignore not_found error since we are unprovision
            try
            {
                if (bindings != null)
                {
                    foreach (ServiceCredentials credential in bindings)
                    {
                        Unbind(credential);
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }

            delete_database(provisioned_service);
            int storage = storage_for_service(provisioned_service);
            this.availableStorage += storage;

            if (!provisioned_service.Destroy())
            {
                Logger.Error(Strings.SqlNodeDeleteServiceErrorMessage, provisioned_service.Name);
                throw new MSSqlError(MSSqlError.MSSqlLocalDBError);
            }

            Logger.Debug(Strings.SqlNodeUnprovisionSuccessDebugMessage, name);
            return true;
        }

        /// <summary>
        /// Binds a SQL Server database to an app.
        /// </summary>
        /// <param name="name">The name of the service.</param>
        /// <param name="bindOptions">Binding options.</param>
        /// <returns>
        /// A new set of credentials used for binding.
        /// </returns>
        protected override ServiceCredentials Bind(string name, Dictionary<string, object> bindOptions)
        {
            return Bind(name, bindOptions, null);
        }

        /// <summary>
        /// Binds a SQL Server database to an app.
        /// </summary>
        /// <param name="name">The name of the service.</param>
        /// <param name="bindOptions">Binding options.</param>
        /// <param name="credentials">Already existing credentials.</param>
        /// <returns>
        /// A new set of credentials used for binding.
        /// </returns>
        protected override ServiceCredentials Bind(string name, Dictionary<string, object> bindOptions, ServiceCredentials credentials)
        {
            Logger.Debug(Strings.SqlNodeBindServiceDebugMessage, name, JsonConvertibleObject.SerializeToJson(bindOptions));
            Dictionary<string, object> binding = null;
            try
            {
                ProvisionedService service = ProvisionedService.GetService(name);
                if (service == null)
                {
                    throw new MSSqlError(MSSqlError.MSSqlConfigNotFound, name);
                }
                // create new credential for binding
                binding = new Dictionary<string, object>();

                if (credentials != null)
                {
                    binding["user"] = credentials.User;
                    binding["password"] = credentials.Password;
                }
                else
                {
                    binding["user"] = "US3R" + Credentials.GenerateCredential();
                    binding["password"] = "P4SS" + Credentials.GenerateCredential();
                }
                binding["bind_opts"] = bindOptions;

                create_database_user(name, binding["user"] as string, binding["password"] as string);
                ServiceCredentials response = gen_credential(name, binding["user"] as string, binding["password"] as string);

                Logger.Debug(Strings.SqlNodeBindResponseDebugMessage, response.SerializeToJson());
                bindingServed += 1;
                return response;
            }
            catch (Exception)
            {
                if (binding != null)
                {
                    delete_database_user(binding["user"] as string);
                }
                throw;
            }
        }

        /// <summary>
        /// Unbinds a SQL Server database from an app.
        /// </summary>
        /// <param name="credentials">The credentials that have to be unprovisioned.</param>
        /// <returns>
        /// A bool indicating whether the unbind request was successful.
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "password"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "bind_opts")]
        protected override bool Unbind(ServiceCredentials credentials)
        {
            if (credentials == null)
            {
                return false;
            }

            Logger.Debug(Strings.SqlNodeUnbindServiceDebugMessage, credentials.SerializeToJson());

            string name = credentials.Name;
            string user = credentials.User;
            Dictionary<string, object> bind_opts = credentials.BindOptions;
            string password = credentials.Password;

            ProvisionedService service = ProvisionedService.GetService(name);
            if (service == null)
            {
                throw new MSSqlError(MSSqlError.MSSqlConfigNotFound, name);
            }
            //todo: vladi: implement this on windows
            // validate the existence of credential, in case we delete a normal account because of a malformed credential
            //res = @connection.select_all("SELECT * from mssql.user WHERE user='#{user}' AND password=PASSWORD('#{passwd}')")
            //raise MsSqlError.new(MsSqlError::MSSQL_CRED_NOT_FOUND, credential.inspect) if res.num_rows()<=0
            delete_database_user(user);
            return true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void create_database(ProvisionedService provisioned_service)
        {
            string name = provisioned_service.Name;
            string password = provisioned_service.Password;
            string user = provisioned_service.User;

            try
            {
                DateTime start = DateTime.Now;
                Logger.Debug(Strings.SqlNodeCreateDatabaseDebugMessage, provisioned_service.SerializeToJson());

                using (SqlCommand createDBCommand = new SqlCommand(String.Format(CultureInfo.InvariantCulture, Strings.SqlNodeCreateDatabaseSQL, name), connection))
                {
                    createDBCommand.ExecuteNonQuery();
                }

                create_database_user(name, user, password);
                int storage = storage_for_service(provisioned_service);
                if (this.availableStorage < storage)
                {
                    throw new MSSqlError(MSSqlError.MSSqlDiskFull);
                }

                this.availableStorage -= storage;
                Logger.Debug(Strings.SqlNodeDoneCreatingDBDebugMessage, provisioned_service.SerializeToJson(), (start - DateTime.Now).TotalSeconds);
            }
            catch (Exception ex)
            {
                Logger.Warning(Strings.SqlNodeCouldNotCreateDBWarningMessage, ex.ToString());
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        private void create_database_user(string name, string user, string password)
        {
            Logger.Info(Strings.SqlNodeCreatingCredentialsInfoMessage, user, password, name);
            

            using (TransactionScope ts = new TransactionScope())
            {
                using (SqlCommand cmdCreateLogin = new SqlCommand(String.Format(CultureInfo.InvariantCulture, Strings.SqlNodeCreateLoginSQL, user, password), connection))
                {
                    cmdCreateLogin.ExecuteNonQuery();
                }

                using (SqlConnection dbConnection = new SqlConnection(ConnectionString))
                {
                    dbConnection.Open();
                    dbConnection.ChangeDatabase(name);

                    using (SqlCommand cmdCreateUser = new SqlCommand(String.Format(CultureInfo.InvariantCulture, Strings.SqlNodeCreateUserSQL, user), dbConnection))
                    {
                        cmdCreateUser.ExecuteNonQuery();
                    }

                    using (SqlCommand cmdAddRoleMember = new SqlCommand("sp_addrolemember", dbConnection))
                    {
                        cmdAddRoleMember.CommandType = CommandType.StoredProcedure;
                        cmdAddRoleMember.Parameters.Add("@rolename", SqlDbType.NVarChar).Value = "db_owner";
                        cmdAddRoleMember.Parameters.Add("@membername", SqlDbType.NVarChar).Value = user;
                        cmdAddRoleMember.ExecuteNonQuery();
                    }
                }
                ts.Complete();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void delete_database(ProvisionedService provisioned_service)
        {
            string name = provisioned_service.Name;
            string user = provisioned_service.User;

            try
            {
                delete_database_user(user);
                Logger.Info(Strings.SqlNodeDeletingDatabaseInfoMessage, name);

                using (TransactionScope ts = new TransactionScope())
                {
                    using (SqlCommand takeOfflineCommand = new SqlCommand(String.Format(CultureInfo.InvariantCulture, Strings.SqlNodeTakeDBOfflineSQL, name), connection))
                    {
                        takeOfflineCommand.ExecuteNonQuery();
                    }
                    using (SqlCommand dropDatabaseCommand = new SqlCommand(String.Format(CultureInfo.InvariantCulture, Strings.SqlNodeDropDatabaseSQL, name), connection))
                    {
                        dropDatabaseCommand.ExecuteNonQuery();
                    }
                    ts.Complete();
                }
            }
            catch (Exception ex)
            {
                Logger.Fatal(Strings.SqlNodeCannotDeleteDBFatalMessage, ex.ToString());
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void delete_database_user(string user)
        {
            Logger.Info(Strings.SqlNodeDeleteUserInfoMessage, user);
            try
            {
                ////Disable the login before kill the users session. So that other connections cannot be made between the killing
                ////of user sessions and dropping the login.
                using (TransactionScope ts = new TransactionScope())
                {
                    using (SqlCommand disableLoginCommand = new SqlCommand(String.Format(CultureInfo.InvariantCulture, Strings.SqlNodeDisableLoginSQL, user), connection))
                    {
                        disableLoginCommand.ExecuteNonQuery();
                    }

                    ts.Complete();
                }

                kill_user_session(user);

                using (TransactionScope ts = new TransactionScope())
                {
                    using (SqlCommand dropLoginCommand = new SqlCommand(String.Format(CultureInfo.InvariantCulture, Strings.SqlNodeDropLoginSQL, user), connection))
                    {
                        dropLoginCommand.ExecuteNonQuery();
                    }

                    ts.Complete();
                }
            }
            catch (Exception ex)
            {
                Logger.Fatal(Strings.SqlNodeCannotDeleteUserFatalMessage, user, ex.ToString());
            }
        }

        // end active sessions for USER, to be able to drop the table
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        private void kill_user_session(string user)
        {
            using (TransactionScope ts = new TransactionScope())
            {
                using (SqlCommand sessionsToKillCommand = new SqlCommand(String.Format(CultureInfo.InvariantCulture, Strings.SqlNodeGetUserSessionsSQL, user), connection))
                {
                    SqlDataReader sessionsToKill = sessionsToKillCommand.ExecuteReader();

                    while (sessionsToKill.Read())
                    {
                        int sessionId = sessionsToKill.GetInt16(0);
                        using (SqlCommand killSessionCommand = new SqlCommand(String.Format(CultureInfo.InvariantCulture, Strings.SqlNodeKillSessionSQL, sessionId), connection))
                        {
                            killSessionCommand.ExecuteNonQuery();
                        }
                    }

                    sessionsToKill.Close();
                }
                ts.Complete();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "database"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        private void kill_database_session(string database)
        {
            //todo: vladi: implement this
        }

        /// <summary>
        /// Restore a given instance using backup file.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="backupPath">The backup path.</param>
        /// <returns>
        /// A bool indicating whether the request was successful.
        /// </returns>
        protected override bool Restore(string instanceId, string backupPath)
        {
            //todo: vladi: implement this
            return false;
        }

        /// <summary>
        /// This methos disables all credentials and kills user sessions.
        /// </summary>
        /// <param name="provisionedCredential">The provisioned credentials.</param>
        /// <param name="bindingCredentials">The binding credentials.</param>
        /// <returns>
        /// A bool indicating whether the request was successful.
        /// </returns>
        protected override bool DisableInstance(ServiceCredentials provisionedCredential, ServiceCredentials bindingCredentials)
        {
            //todo: vladi: Replace with code for odbc object for SQL Server
            return false;
        }

        /// <summary>
        /// Dumps database content into a given path.
        /// </summary>
        /// <param name="provisionedCredential">The provisioned credential.</param>
        /// <param name="bindingCredentials">The binding credentials.</param>
        /// <param name="filePath">The file path where to dump the service.</param>
        /// <returns>
        /// A bool indicating whether the request was successful.
        /// </returns>
        protected override bool DumpInstance(ServiceCredentials provisionedCredential, ServiceCredentials bindingCredentials, string filePath)
        {
            //todo: vladi: Replace with code for odbc object for SQL Server
            return false;
        }

        /// <summary>
        /// Imports an instance from a path.
        /// </summary>
        /// <param name="provisionedCredential">The provisioned credential.</param>
        /// <param name="bindingCredentials">The binding credentials.</param>
        /// <param name="filePath">The file path from which to import the service.</param>
        /// <param name="plan">The payment plan.</param>
        /// <returns>
        /// A bool indicating whether the request was successful.
        /// </returns>
        protected override bool ImportInstance(ServiceCredentials provisionedCredential, ServiceCredentials bindingCredentials, string filePath, ProvisionedServicePlanType plan)
        {
            //todo: vladi: Replace with code for odbc object for SQL Server
            return false;
        }

        /// <summary>
        /// Re-enables the instance, re-binds credentials.
        /// </summary>
        /// <param name="provisionedCredential">The provisioned credential.</param>
        /// <param name="bindingCredentialsHash">The binding credentials hash.</param>
        /// <returns>
        /// A bool indicating whether the request was successful.
        /// </returns>
        protected override bool EnableInstance(ref ServiceCredentials provisionedCredential, ref Dictionary<string, object> bindingCredentialsHash)
        {
            //todo: vladi: Replace with code for odbc object for SQL Server
            return false;
        }

        /// <summary>
        /// Gets varz details about the SQL Server Node.
        /// </summary>
        /// <returns>
        /// A dictionary containing varz details.
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        protected override Dictionary<string, object> VarzDetails()
        {
            Logger.Debug(Strings.SqlNodeGenerateVarzDebugMessage);
            Dictionary<string, object> varz = new Dictionary<string, object>();
            try
            {
                // how many queries served since startup
                varz["queries_since_startup"] = get_queries_status();
                // queries per second
                varz["queries_per_second"] = get_qps();
                // disk usage per instance
                object[] status = get_instance_status();
                varz["database_status"] = status;
                // node capacity
                varz["node_storage_capacity"] = this.nodeCapacity;
                varz["node_storage_used"] = this.nodeCapacity - this.availableStorage;
                // how many long queries and long txs are killed.
                varz["long_queries_killed"] = longQueriesKilled;
                varz["long_transactions_killed"] = longTxKilled;
                // how many provision/binding operations since startup.
                varz["provision_served"] = provisionServed;
                varz["binding_served"] = bindingServed;
                return varz;
            }
            catch (Exception ex)
            {
                Logger.Error(Strings.SqlNodeGenerateVarzErrorMessage, ex.ToString());
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Gets healthz details about the SQL Server Node.
        /// </summary>
        /// <returns>
        /// A dictionary containing healthz details.
        /// </returns>
        protected override Dictionary<string, string> HealthzDetails()
        {
            //todo: vladi: Replace with code for odbc object for SQL Server
            Dictionary<string, string> healthz = new Dictionary<string, string>()
            {
                {"self", "ok"}
            };

            return healthz;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "instance"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        private string get_instance_healthz(ServiceCredentials instance)
        {
            //todo: vladi: implement this
            string res = "ok";
            return res;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        private int get_queries_status()
        {
            //todo: vladi: implement this
            return 0;
        }

        private double get_qps()
        {
            Logger.Debug(Strings.SqlNodeCalculatingQPSDebugMessage);
            int queries = get_queries_status();
            DateTime ts = DateTime.Now;

            double delta_t = (ts - this.qpsLastUpdated).TotalSeconds;

            double qps = (queries - @queriesServed) / delta_t;
            this.queriesServed = queries;
            this.qpsLastUpdated = ts;

            return qps;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        private object[] get_instance_status()
        {
            //todo: vladi: implement this
            return new object[0];
        }

        private ServiceCredentials gen_credential(string name, string user, string passwd)
        {
            ServiceCredentials response = new ServiceCredentials();

            response.Name = name;
            response.HostName = localIp;
            response.HostName = localIp;
            response.Port = mssqlConfig.Port;
            response.User = user;
            response.UserName = user;
            response.Password = passwd;

            return response;
        }
    }
}