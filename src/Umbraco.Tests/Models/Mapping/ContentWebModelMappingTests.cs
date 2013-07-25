﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.PropertyEditors;
using Umbraco.Tests.TestHelpers;
using Umbraco.Tests.TestHelpers.Entities;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Models.Mapping;

namespace Umbraco.Tests.Models.Mapping
{
    [TestFixture]
    public class ContentWebModelMappingTests : BaseDatabaseFactoryTest
    {
        [PropertyEditor("00000000-0000-0000-0000-000000000000", "Test", "~/Test.html")]
        public class TestPropertyEditor : PropertyEditor
        {
            
        }

        protected override DatabaseBehavior DatabaseTestBehavior
        {
            get { return DatabaseBehavior.NewSchemaPerFixture; }
        }

        protected override void FreezeResolution()
        {
            PropertyEditorResolver.Current = new PropertyEditorResolver(
                () => new List<Type> {typeof (TestPropertyEditor)});

            base.FreezeResolution();
        }

        [Test]
        public void To_Media_Item_Simple()
        {
            var contentType = MockedContentTypes.CreateImageMediaType();
            var content = MockedMedia.CreateMediaImage(contentType, -1);

            var result = Mapper.Map<IMedia, ContentItemBasic<ContentPropertyBasic, IMedia>>(content);

            AssertBasics(result, content);

            foreach (var p in content.Properties)
            {
                AssertBasicProperty(result, p);
            }
        }

        [Test]
        public void To_Content_Item_Simple()
        {
            var contentType = MockedContentTypes.CreateSimpleContentType();
            var content = MockedContent.CreateSimpleContent(contentType);

            var result = Mapper.Map<IContent, ContentItemBasic<ContentPropertyBasic, IContent>>(content);

            AssertBasics(result, content);

            foreach (var p in content.Properties)
            {
                AssertBasicProperty(result, p);
            }
        }

        [Test]
        public void To_Content_Item_Dto()
        {
            var contentType = MockedContentTypes.CreateSimpleContentType();
            var content = MockedContent.CreateSimpleContent(contentType);

            var result = Mapper.Map<IContent, ContentItemDto<IContent>>(content);

            AssertContentItem(result, content);    
        }

        [Test]
        public void To_Media_Item_Dto()
        {
            var contentType = MockedContentTypes.CreateImageMediaType();
            var content = MockedMedia.CreateMediaImage(contentType, -1);

            var result = Mapper.Map<IMedia, ContentItemDto<IMedia>>(content);

            AssertContentItem(result, content);
        }

        #region Assertions
        private void AssertBasics<T, TPersisted>(ContentItemBasic<T, TPersisted> result, TPersisted content)
            where T : ContentPropertyBasic
            where TPersisted : IContentBase
        {
            Assert.AreEqual(content.Id, result.Id);
            Assert.AreEqual(0, result.Owner.UserId);
            Assert.AreEqual("admin", result.Owner.Name);
            Assert.AreEqual(content.ParentId, result.ParentId);
            Assert.AreEqual(content.UpdateDate, result.UpdateDate);
            Assert.AreEqual(content.CreateDate, result.CreateDate);
            Assert.AreEqual(content.Name, result.Name);
            Assert.AreEqual(content.Properties.Count(), result.Properties.Count());
        }

        private void AssertBasicProperty<T, TPersisted>(ContentItemBasic<T, TPersisted> result, Property p)
            where T : ContentPropertyBasic
            where TPersisted : IContentBase
        {
            var pDto = result.Properties.SingleOrDefault(x => x.Alias == p.Alias);
            Assert.IsNotNull(pDto);
            Assert.AreEqual(p.Alias, pDto.Alias);
            Assert.AreEqual(p.Id, pDto.Id);
            Assert.AreEqual(p.Value, pDto.Value);
        }

        private void AssertProperty<TPersisted>(ContentItemBasic<ContentPropertyDto, TPersisted> result, Property p)
            where TPersisted : IContentBase
        {
            AssertBasicProperty(result, p);

            var pDto = result.Properties.SingleOrDefault(x => x.Alias == p.Alias);
            Assert.IsNotNull(pDto);
            Assert.AreEqual(p.PropertyType.Mandatory, pDto.IsRequired);
            Assert.AreEqual(p.PropertyType.ValidationRegExp, pDto.ValidationRegExp);
            Assert.AreEqual(p.PropertyType.Description, pDto.Description);
            Assert.AreEqual(p.PropertyType.Name, pDto.Label);
            Assert.AreEqual(ApplicationContext.Services.DataTypeService.GetDataTypeDefinitionById(p.PropertyType.DataTypeDefinitionId), pDto.DataType);
            Assert.AreEqual(PropertyEditorResolver.Current.GetById(p.PropertyType.DataTypeId), pDto.PropertyEditor);
        }

        private void AssertContentItem<T>(ContentItemBasic<ContentPropertyDto, T> result, T content)
            where T : IContentBase
        {
            AssertBasics(result, content);

            foreach (var p in content.Properties)
            {
                AssertProperty(result, p);
            }
        } 
        #endregion

        [Test]
        public void To_Display_Model()
        {
            var contentType = MockedContentTypes.CreateSimpleContentType();
            var content = MockedContent.CreateSimpleContent(contentType);

            var mapper = new ContentModelMapper(ApplicationContext, new UserModelMapper());

            var result = Mapper.Map<IContent, ContentItemDisplay>(content);

            Assert.AreEqual(content.Name, result.Name);
            Assert.AreEqual(content.Id, result.Id);
            Assert.AreEqual(content.Properties.Count(), result.Properties.Count());
            Assert.AreEqual(content.PropertyGroups.Count(), result.Tabs.Count() - 1);
            Assert.IsTrue(result.Tabs.Any(x => x.Label == "Generic properties"));
            Assert.IsTrue(result.Tabs.First().IsActive);
            Assert.IsTrue(result.Tabs.Except(new[] {result.Tabs.First()}).All(x => x.IsActive == false));
        }

        [Test]
        public void To_Display_Model_With_Non_Grouped_Properties()
        {
            var idSeed = 1;
            var contentType = MockedContentTypes.CreateSimpleContentType();            
            //add non-grouped properties
            contentType.AddPropertyType(new PropertyType(new Guid(), DataTypeDatabaseType.Ntext) { Alias = "nonGrouped1", Name = "Non Grouped 1", Description = "", Mandatory = false, SortOrder = 1, DataTypeDefinitionId = -88 });
            contentType.AddPropertyType(new PropertyType(new Guid(), DataTypeDatabaseType.Ntext) { Alias = "nonGrouped2", Name = "Non Grouped 2", Description = "", Mandatory = false, SortOrder = 1, DataTypeDefinitionId = -88 });
            //set ids or it wont work
            contentType.Id = idSeed;
            foreach (var p in contentType.PropertyTypes)
            {
                p.Id = idSeed;
                idSeed++;
            }
            var content = MockedContent.CreateSimpleContent(contentType);
            foreach (var p in content.Properties)
            {
                p.Id = idSeed;
                idSeed++;
            }
            //ensure that nothing is marked as dirty
            contentType.ResetDirtyProperties(false);
            //ensure that nothing is marked as dirty
            content.ResetDirtyProperties(false);

            var mapper = new ContentModelMapper(ApplicationContext, new UserModelMapper());

            var result = Mapper.Map<IContent, ContentItemDisplay>(content);

            Assert.AreEqual(content.Name, result.Name);
            Assert.AreEqual(content.Id, result.Id);
            Assert.AreEqual(content.Properties.Count(), result.Properties.Count());
            Assert.AreEqual(content.PropertyGroups.Count(), result.Tabs.Count() - 1);
            Assert.IsTrue(result.Tabs.Any(x => x.Label == "Generic properties"));
            Assert.AreEqual(2, result.Tabs.Where(x => x.Label == "Generic properties").SelectMany(x => x.Properties).Count());
        }
    }
}