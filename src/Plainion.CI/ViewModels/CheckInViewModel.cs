﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows.Input;
using Plainion.CI.Services;
using Plainion.CI.Services.SourceControl;
using Plainion.Collections;
using Plainion.Windows.Mvvm;

namespace Plainion.CI.ViewModels
{
    [Export]
    class CheckInViewModel : BindableBase
    {
        private BuildService myBuildService;
        private ISourceControl mySourceControl;
        private RepositoryEntry mySelectedFile;
        private string myCheckInComment;
        private PendingChangesObserver myPendingChangesObserver;

        [ImportingConstructor]
        public CheckInViewModel(BuildService buildService, ISourceControl sourceControl)
        {
            myBuildService = buildService;
            mySourceControl = sourceControl;

            Files = new ObservableCollection<RepositoryEntry>();

            RefreshCommand = new DelegateCommand(RefreshPendingChanges);
            RevertCommand = new DelegateCommand<string>(OnRevert);

            IgnoreFileCommand = new DelegateCommand<string>(OnIgnoreFile);
            IgnoreExtensionCommand = new DelegateCommand<string>(OnIgnoreExtension);
            IgnoreDirectoryCommand = new DelegateCommand<string>(OnIgnoreDirectory);

            DiffToPreviousCommand = new DelegateCommand(OnDiffToPrevious, CanDiffToPrevious);

            myPendingChangesObserver = new PendingChangesObserver(mySourceControl, OnPendingChangesChanged);

            buildService.BuildDefinitionChanged += OnBuildDefinitionChanged;
            OnBuildDefinitionChanged();
        }

        private void OnPendingChangesChanged(IEnumerable<Change> pendingChanges)
        {
            var uncheckedFiles = Files
                .Where(f => !f.IsChecked)
                .Select(f => f.File)
                .ToList();

            var files = pendingChanges
                .Select(e => new RepositoryEntry(e) { IsChecked = !uncheckedFiles.Contains(e.Path) })
                .OrderBy(e => e.File);

            Files.Clear();
            Files.AddRange(files);
        }

        private void OnBuildDefinitionChanged()
        {
            if (BuildDefinition != null)
            {
                BuildDefinition.PropertyChanged -= BuildDefinition_PropertyChanged;
            }

            myPendingChangesObserver.Stop();

            BuildDefinition = myBuildService.BuildDefinition;
            OnPropertyChanged(nameof(BuildDefinition));

            if (BuildDefinition != null)
            {
                BuildDefinition.PropertyChanged += BuildDefinition_PropertyChanged;
                OnRepositoryRootChanged();
            }

            DiffToPreviousCommand.RaiseCanExecuteChanged();
        }

        private void BuildDefinition_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BuildDefinition.RepositoryRoot))
            {
                OnRepositoryRootChanged();
            }
            else if (e.PropertyName == nameof(BuildDefinition.DiffTool))
            {
                DiffToPreviousCommand.RaiseCanExecuteChanged();
            }
        }

        private void OnRepositoryRootChanged()
        {
            myPendingChangesObserver.Stop();

            if (!string.IsNullOrEmpty(BuildDefinition.RepositoryRoot) && Directory.Exists(BuildDefinition.RepositoryRoot))
            {
                myPendingChangesObserver.Start(myBuildService.BuildDefinition.RepositoryRoot);
            }
        }

        public BuildDefinition BuildDefinition { get; private set; }

        public ObservableCollection<RepositoryEntry> Files { get; private set; }

        public RepositoryEntry SelectedFile
        {
            get { return mySelectedFile; }
            set { SetProperty(ref mySelectedFile, value); }
        }

        public string CheckInComment
        {
            get { return myCheckInComment; }
            set { SetProperty(ref myCheckInComment, value); }
        }

        public SecureString SecurePassword { get; set; }

        public ICommand RefreshCommand { get; private set; }

        public async void RefreshPendingChanges()
        {
            if (string.IsNullOrEmpty(BuildDefinition.RepositoryRoot) || !Directory.Exists(BuildDefinition.RepositoryRoot))
            {
                return;
            }

            var pendingChanges = await mySourceControl.GetPendingChangesAsync(BuildDefinition.RepositoryRoot);

            OnPendingChangesChanged(pendingChanges);
        }

        public ICommand RevertCommand { get; private set; }

        private void OnRevert(string file)
        {
            mySourceControl.Revert(BuildDefinition.RepositoryRoot, file);
        }

        public ICommand IgnoreFileCommand { get; private set; }

        private void OnIgnoreFile(string file)
        {
            mySourceControl.Ignore(BuildDefinition.RepositoryRoot, "/" + file);
        }

        public ICommand IgnoreExtensionCommand { get; private set; }

        private void OnIgnoreExtension(string file)
        {
            mySourceControl.Ignore(BuildDefinition.RepositoryRoot, "*" + Path.GetExtension(file));
        }

        public ICommand IgnoreDirectoryCommand { get; private set; }

        private void OnIgnoreDirectory(string file)
        {
            mySourceControl.Ignore(BuildDefinition.RepositoryRoot, "/" + Path.GetDirectoryName(file));
        }

        public DelegateCommand DiffToPreviousCommand { get; private set; }

        private bool CanDiffToPrevious()
        {
            return BuildDefinition != null && !string.IsNullOrEmpty(BuildDefinition.DiffTool);
        }

        public void OnDiffToPrevious()
        {
            mySourceControl.DiffToPrevious(BuildDefinition.RepositoryRoot, SelectedFile.File, BuildDefinition.DiffTool);
        }
    }
}
